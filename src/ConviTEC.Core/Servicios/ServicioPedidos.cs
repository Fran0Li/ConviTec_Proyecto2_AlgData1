using System;
using System.Threading;
using System.Threading.Tasks;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Servicios;

// Registro de pedidos en la SEDE RECEPTORA (diseño 5.6, Tabla 11, D-01, D-02, D-03, D-06).
//
//   1. Validar las líneas (productos del catálogo, cantidades mayores que cero; las repetidas se suman).
//   2. Ruta primero (D-01): si el destino no es alcanzable, se rechaza sin reservar nada.
//   3. Asignar proveedores (D-02): primero esta sede; lo que falte, a la sede con más disponible
//      según la copia global, y se puede repartir entre varias.
//   4. Reservar lo local y mandar SolicitudReserva a cada sede que pone productos.
//   5. Esperar las respuestas, como mucho 10 s (D-03):
//        - todas aceptadas → confirmar lo local, mandar ConfirmarReserva, pedido Pendiente;
//        - alguna rechaza o no contesta → liberar lo local, mandar LiberarReserva, pedido Cancelado.
//   6. Una respuesta que llega tarde, de un pedido ya cancelado, se contesta con LiberarReserva.
//
// El lado de la sede dueña (atender las solicitudes) es ServicioReservas.
public class ServicioPedidos
{
    public static readonly TimeSpan TiempoLimitePorDefecto = TimeSpan.FromSeconds(10);

    private readonly string codigoSede;
    private readonly string ubicacionSede;
    private readonly IInventarioReservas inventario;
    private readonly IInventarioGlobal global;
    private readonly IRegistroPedidos registro;
    private readonly ServicioRutas rutas;
    private readonly IBusMensajes bus;
    private readonly TimeSpan tiempoLimite;
    private readonly Func<DateTime> reloj;
    private readonly Action<string> registrar;

    // Protege el inventario local y las esperas activas: la API (pedidos nuevos) y RabbitMQ
    // (respuestas) corren en hilos distintos. Nunca se espera la red con el candado tomado.
    private readonly SemaphoreSlim candado = new SemaphoreSlim(1, 1);

    // Pedidos que están esperando respuestas de reserva ahora mismo.
    private EsperaReservas?[] esperas = new EsperaReservas?[4];
    private int cantidadEsperas;

    // ubicacionSede: el nombre de esta sede en el grafo de rutas (por ejemplo "Limón").
    // tiempoLimite: cuánto se esperan las respuestas (D-03). En las pruebas se usa uno corto.
    public ServicioPedidos(string codigoSede, string ubicacionSede, IInventarioReservas inventario,
                           IInventarioGlobal global, IRegistroPedidos registro, ServicioRutas rutas,
                           IBusMensajes bus, TimeSpan? tiempoLimite = null, Func<DateTime>? reloj = null,
                           Action<string>? registrar = null)
    {
        if (string.IsNullOrWhiteSpace(codigoSede))
            throw new ArgumentException("Falta el código de la sede.", nameof(codigoSede));
        if (string.IsNullOrWhiteSpace(ubicacionSede))
            throw new ArgumentException("Falta la ubicación de la sede en el grafo.", nameof(ubicacionSede));

        this.codigoSede = codigoSede;
        this.ubicacionSede = ubicacionSede;
        this.inventario = inventario ?? throw new ArgumentNullException(nameof(inventario));
        this.global = global ?? throw new ArgumentNullException(nameof(global));
        this.registro = registro ?? throw new ArgumentNullException(nameof(registro));
        this.rutas = rutas ?? throw new ArgumentNullException(nameof(rutas));
        this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        this.tiempoLimite = tiempoLimite ?? TiempoLimitePorDefecto;
        if (this.tiempoLimite <= TimeSpan.Zero)
            throw new ArgumentException("El tiempo límite debe ser positivo.", nameof(tiempoLimite));
        this.reloj = reloj ?? (() => DateTime.Now);
        this.registrar = registrar ?? Console.WriteLine;
    }

    // Le dice al despachador que las respuestas de reserva son para este servicio.
    public void RegistrarEn(Despachador despachador)
    {
        if (despachador == null)
            throw new ArgumentNullException(nameof(despachador));
        despachador.Registrar(TipoMensaje.RespuestaReserva, AlRecibirRespuestaAsync);
    }

    // Id de la reserva que este pedido le hace a una sede: "LIM-0042-SJO".
    public static string IdReserva(string idPedido, string sedeDuena)
    {
        return idPedido + "-" + sedeDuena;
    }

    // ==================== Registrar un pedido ====================

    // Devuelve cuando el pedido ya quedó decidido (como mucho, el tiempo límite después).
    // Lanza excepción solo por errores de verdad (por ejemplo, no se pudo guardar el archivo).
    public async Task<ResultadoPedido> RegistrarAsync(string destino, LineaPedido[] lineas)
    {
        // 1. Validar las líneas (sin tocar nada todavía).
        string[] codigos;
        int[] cantidades;
        string? error = Agrupar(lineas, out codigos, out cantidades);
        if (error != null)
            return ResultadoPedido.Rechazo(error);

        var productos = new Producto[codigos.Length];
        for (int i = 0; i < codigos.Length; i++)
        {
            Producto? producto = global.BuscarProducto(codigos[i]);
            if (producto == null)
                return ResultadoPedido.Rechazo("El producto " + codigos[i] + " no existe en el catálogo.");
            productos[i] = producto;
        }

        // 2. Ruta primero (D-01). También rechaza que el destino sea la misma sede (D-06).
        ValidacionRuta ruta = rutas.ValidarDestino(ubicacionSede, destino);
        if (!ruta.Valida)
            return ResultadoPedido.Rechazo(ruta.Mensaje);

        // 3 y 4. Asignar proveedores y reservar lo local, con el candado.
        Pedido pedido;
        LineaReserva[] locales;
        string[] remotas;
        EsperaReservas? espera = null;

        await candado.WaitAsync();
        try
        {
            DetallePedido[]? detalles = Asignar(productos, cantidades, out string motivo);
            if (detalles == null)
                return ResultadoPedido.Rechazo(motivo);

            // Sin RabbitMQ solo se aceptan pedidos que se cubren con esta sede (Tabla 16).
            if (UsaOtrasSedes(detalles) && !bus.Conectado)
                return ResultadoPedido.Rechazo("No hay comunicación con las demás sedes. Solo se aceptan " +
                                               "pedidos que se cubren con el inventario de esta sede.");

            string id = codigoSede + "-" + registro.SiguienteNumero().ToString("D4");
            pedido = new Pedido(id, codigoSede, destino, reloj(), detalles);
            locales = LineasReserva(pedido.DetallesDeSede(codigoSede));
            remotas = SedesRemotas(pedido);

            if (locales.Length > 0 && !inventario.Reservar(locales, out motivo))
                return ResultadoPedido.Rechazo(motivo);

            try
            {
                if (remotas.Length == 0)
                {
                    // Todo sale de esta sede: no hay a quién esperar.
                    inventario.Confirmar(locales);
                    pedido.Confirmar();
                    registro.Guardar(pedido);
                }
                else
                {
                    registro.Guardar(pedido);   // queda Registrado mientras llegan las respuestas
                    espera = new EsperaReservas(pedido, locales, remotas);
                    AgregarEspera(espera);
                }
            }
            catch
            {
                // No se pudo guardar: se devuelve lo apartado para no dejar unidades bloqueadas.
                inventario.Liberar(locales);
                throw;
            }
        }
        finally
        {
            candado.Release();
        }

        if (espera == null)
        {
            Avisar("Pedido " + pedido.Id + " confirmado con inventario local.");
            await DifundirDisponiblesAsync(locales);
            return new ResultadoPedido(true, MensajeAceptado(pedido, ruta), pedido);
        }

        // 4b. Pedir las reservas. Si el envío falla, el pedido se cancela de una vez.
        LineaMensaje[][] porSede = new LineaMensaje[remotas.Length][];
        for (int i = 0; i < remotas.Length; i++)
            porSede[i] = LineasMensaje(pedido.DetallesDeSede(remotas[i]));

        for (int i = 0; i < remotas.Length; i++)
        {
            try
            {
                var solicitud = new SolicitudReserva(IdReserva(pedido.Id, remotas[i]), pedido.Id, porSede[i]);
                await bus.EnviarAsync(Sobre.ParaSede(codigoSede, remotas[i], solicitud));
            }
            catch (Exception e)
            {
                espera.Fallar("No se pudo pedir la reserva a " + remotas[i] + ": " + e.Message);
                break;
            }
        }

        // 5. Esperar respuestas o el tiempo límite, lo que pase primero.
        await Task.WhenAny(espera.Listo.Task, Task.Delay(tiempoLimite));
        return await CerrarAsync(espera, ruta);
    }

    // Decide el pedido: confirma si todas aceptaron; si no, cancela y libera todo.
    private async Task<ResultadoPedido> CerrarAsync(EsperaReservas espera, ValidacionRuta ruta)
    {
        Pedido pedido = espera.Pedido;
        bool aceptado;
        string motivo = "";

        await candado.WaitAsync();
        try
        {
            QuitarEspera(espera);
            aceptado = espera.TodasAceptadas;
            if (aceptado)
            {
                if (espera.Locales.Length > 0)
                    inventario.Confirmar(espera.Locales);
                pedido.Confirmar();
            }
            else
            {
                motivo = espera.MotivoRechazo ?? ("No respondió a tiempo: " + espera.SinResponder() +
                         " (se esperó " + tiempoLimite.TotalSeconds + " s).");
                if (espera.Locales.Length > 0)
                    inventario.Liberar(espera.Locales);
                pedido.Cancelar(motivo);
            }
            registro.Guardar(pedido);
        }
        finally
        {
            candado.Release();
        }

        if (aceptado)
        {
            // Fase 2 en las sedes dueñas. Si un envío falla solo se anota: el pedido ya está
            // confirmado aquí, y el mensaje persistente sigue en RabbitMQ si la conexión volvió.
            for (int i = 0; i < espera.Remotas.Length; i++)
                await EnviarSinFallarAsync(espera.Remotas[i], new ConfirmarReserva(IdReserva(pedido.Id, espera.Remotas[i])));
            await DifundirDisponiblesAsync(espera.Locales);
            Avisar("Pedido " + pedido.Id + " confirmado.");
            return new ResultadoPedido(true, MensajeAceptado(pedido, ruta), pedido);
        }

        // Se libera en todas las que no rechazaron: también en las que no contestaron, porque quizá
        // sí apartaron y la respuesta no llegó. Liberar algo que no existe no hace daño (la sede
        // dueña lo ignora), y si este mensaje se perdiera, su reserva vence sola a los 5 minutos.
        for (int i = 0; i < espera.Remotas.Length; i++)
        {
            if (!espera.Rechazo(i))
                await EnviarSinFallarAsync(espera.Remotas[i], new LiberarReserva(IdReserva(pedido.Id, espera.Remotas[i]), "Pedido cancelado: " + motivo));
        }
        Avisar("Pedido " + pedido.Id + " cancelado: " + motivo);
        return new ResultadoPedido(false, "El pedido se canceló. " + motivo, pedido);
    }

    // ==================== Respuestas que llegan por RabbitMQ ====================

    public async Task AlRecibirRespuestaAsync(Sobre sobre)
    {
        var respuesta = (RespuestaReserva)sobre.Contenido;
        string sede = sobre.SedeOrigen;
        bool liberarTarde = false;

        await candado.WaitAsync();
        try
        {
            EsperaReservas? espera = BuscarEspera(respuesta.IdPedido);
            if (espera != null)
            {
                espera.Registrar(sede, respuesta.Aceptada, respuesta.Motivo);
            }
            else if (respuesta.Aceptada)
            {
                // Ya no se está esperando este pedido. Si se canceló (o no existe), esas unidades
                // quedaron apartadas en la otra sede sin motivo: se le pide liberarlas (D-03).
                Pedido? pedido = registro.Buscar(respuesta.IdPedido);
                liberarTarde = pedido == null || pedido.Estado == EstadoPedido.Cancelado;
                Avisar("Respuesta tardía de " + sede + " para " + respuesta.IdPedido +
                       (liberarTarde ? "; se le pide liberar." : "; se ignora."));
            }
        }
        finally
        {
            candado.Release();
        }

        // Si este envío falla, la excepción devuelve la respuesta a la cola y se reintenta.
        if (liberarTarde)
            await bus.EnviarAsync(Sobre.ParaSede(codigoSede, sede,
                new LiberarReserva(respuesta.IdReserva, "El pedido ya había sido cancelado.")));
    }

    // ==================== Al arrancar la sede ====================

    // Cancela los pedidos que quedaron esperando reservas cuando la sede se apagó (diseño 5.10,
    // paso 4): libera lo local y pide a las otras sedes que liberen lo suyo. Devuelve cuántos canceló.
    public async Task<int> CancelarPedidosEnEsperaAsync()
    {
        const string Motivo = "La sede se reinició mientras esperaba las reservas.";
        Pedido[] enEspera;
        await candado.WaitAsync();
        try
        {
            enEspera = registro.EnEspera();
            for (int i = 0; i < enEspera.Length; i++)
            {
                LineaReserva[] locales = LineasReserva(enEspera[i].DetallesDeSede(codigoSede));
                try
                {
                    if (locales.Length > 0)
                        inventario.Liberar(locales);
                }
                catch (InvalidOperationException e)
                {
                    // El inventario no tenía esas unidades apartadas (por ejemplo, no se guardó el
                    // apartado antes de apagarse). No hay nada que devolver.
                    Avisar("Pedido " + enEspera[i].Id + ": no se pudo liberar lo local (" + e.Message + ").");
                }
                enEspera[i].Cancelar(Motivo);
                registro.Guardar(enEspera[i]);
            }
        }
        finally
        {
            candado.Release();
        }

        for (int i = 0; i < enEspera.Length; i++)
        {
            string[] remotas = SedesRemotas(enEspera[i]);
            for (int j = 0; j < remotas.Length; j++)
                await EnviarSinFallarAsync(remotas[j], new LiberarReserva(IdReserva(enEspera[i].Id, remotas[j]), Motivo));
        }
        return enEspera.Length;
    }

    // ==================== Asignación de proveedores (D-02) ====================

    // Arma las líneas del pedido diciendo de qué sede sale cada parte. Devuelve null (y el motivo)
    // si ni sumando toda la cadena alcanza algún producto.
    private DetallePedido[]? Asignar(Producto[] productos, int[] cantidades, out string motivo)
    {
        string[] sedes = global.Sedes();
        // Como mucho, cada producto sale de esta sede más todas las demás.
        var temporal = new DetallePedido[productos.Length * (sedes.Length + 1)];
        int cantidad = 0;

        for (int i = 0; i < productos.Length; i++)
        {
            Producto p = productos[i];
            int faltante = cantidades[i];

            // Primero esta sede, con su inventario real (no la copia global).
            int local = DisponibleLocal(p.Codigo);
            int total = local;
            int tomar = Math.Min(faltante, local);
            if (tomar > 0)
            {
                temporal[cantidad++] = new DetallePedido(p.Codigo, p.Nombre, tomar, p.Precio, codigoSede);
                faltante -= tomar;
            }

            // Lo que falte: siempre a la sede con más disponible entre las que quedan.
            bool[] usada = new bool[sedes.Length];
            while (faltante > 0)
            {
                int mejor = -1;
                int mejorDisponible = 0;
                for (int s = 0; s < sedes.Length; s++)
                {
                    if (usada[s] || sedes[s] == codigoSede)
                        continue;
                    int disponible = global.Disponible(sedes[s], p.Codigo);
                    if (disponible > mejorDisponible)
                    {
                        mejor = s;
                        mejorDisponible = disponible;
                    }
                }

                if (mejor < 0)
                {
                    motivo = "No hay suficientes unidades de " + p.Nombre + " (" + p.Codigo + ") en la cadena: " +
                             "se piden " + cantidades[i] + " y hay " + (total + SumaOtras(sedes, p.Codigo)) + ".";
                    return null;
                }

                usada[mejor] = true;
                tomar = Math.Min(faltante, mejorDisponible);
                temporal[cantidad++] = new DetallePedido(p.Codigo, p.Nombre, tomar, p.Precio, sedes[mejor]);
                faltante -= tomar;
            }
        }

        var resultado = new DetallePedido[cantidad];
        Array.Copy(temporal, resultado, cantidad);
        motivo = "";
        return resultado;
    }

    private int SumaOtras(string[] sedes, string codigoProducto)
    {
        int suma = 0;
        for (int s = 0; s < sedes.Length; s++)
        {
            if (sedes[s] != codigoSede)
                suma += global.Disponible(sedes[s], codigoProducto);
        }
        return suma;
    }

    private int DisponibleLocal(string codigoProducto)
    {
        try
        {
            return inventario.Disponible(codigoProducto);
        }
        catch (ArgumentException)
        {
            return 0;   // esta sede no tiene el producto en su inventario
        }
    }

    // ==================== Ayudantes ====================

    // Revisa las líneas y suma las del mismo producto (Tabla 16). Devuelve el error o null.
    private static string? Agrupar(LineaPedido[]? lineas, out string[] codigos, out int[] cantidades)
    {
        codigos = new string[0];
        cantidades = new int[0];
        if (lineas == null || lineas.Length == 0)
            return "El pedido debe tener al menos un producto.";

        var temporalCodigos = new string[lineas.Length];
        var temporalCantidades = new int[lineas.Length];
        int distintos = 0;

        for (int i = 0; i < lineas.Length; i++)
        {
            if (lineas[i] == null || string.IsNullOrWhiteSpace(lineas[i].CodigoProducto))
                return "La línea " + (i + 1) + " no indica el producto.";
            string codigo = lineas[i].CodigoProducto.Trim();
            if (lineas[i].Cantidad <= 0)
                return "La cantidad de " + codigo + " debe ser un número entero mayor que cero.";

            int j = 0;
            while (j < distintos && temporalCodigos[j] != codigo)
                j++;
            if (j == distintos)
            {
                temporalCodigos[distintos] = codigo;
                distintos++;
            }
            temporalCantidades[j] += lineas[i].Cantidad;
        }

        codigos = new string[distintos];
        cantidades = new int[distintos];
        Array.Copy(temporalCodigos, codigos, distintos);
        Array.Copy(temporalCantidades, cantidades, distintos);
        return null;
    }

    private bool UsaOtrasSedes(DetallePedido[] detalles)
    {
        for (int i = 0; i < detalles.Length; i++)
        {
            if (detalles[i].CodigoSedeOrigen != codigoSede)
                return true;
        }
        return false;
    }

    // Sedes que ponen productos, sin contar esta.
    private string[] SedesRemotas(Pedido pedido)
    {
        string[] participantes = pedido.SedesParticipantes();
        var temporal = new string[participantes.Length];
        int cantidad = 0;
        for (int i = 0; i < participantes.Length; i++)
        {
            if (participantes[i] != codigoSede)
                temporal[cantidad++] = participantes[i];
        }
        var resultado = new string[cantidad];
        Array.Copy(temporal, resultado, cantidad);
        return resultado;
    }

    private static LineaReserva[] LineasReserva(DetallePedido[] detalles)
    {
        var lineas = new LineaReserva[detalles.Length];
        for (int i = 0; i < detalles.Length; i++)
            lineas[i] = new LineaReserva(detalles[i].CodigoProducto, detalles[i].Cantidad);
        return lineas;
    }

    private static LineaMensaje[] LineasMensaje(DetallePedido[] detalles)
    {
        var lineas = new LineaMensaje[detalles.Length];
        for (int i = 0; i < detalles.Length; i++)
            lineas[i] = new LineaMensaje(detalles[i].CodigoProducto, detalles[i].Cantidad);
        return lineas;
    }

    private static string MensajeAceptado(Pedido pedido, ValidacionRuta ruta)
    {
        return "Pedido " + pedido.Id + " registrado. Se entrega en " + pedido.Destino +
               " (" + ruta.Tiempo + " min desde la sede).";
    }

    // Avisa a las demás sedes cuánto queda disponible aquí de los productos que se descontaron.
    private async Task DifundirDisponiblesAsync(LineaReserva[] lineas)
    {
        if (lineas.Length == 0 || !bus.Conectado)
            return;
        try
        {
            var disponibles = new LineaMensaje[lineas.Length];
            for (int i = 0; i < lineas.Length; i++)
                disponibles[i] = new LineaMensaje(lineas[i].CodigoProducto, inventario.Disponible(lineas[i].CodigoProducto));
            await bus.EnviarAsync(Sobre.ParaTodas(codigoSede, new ActualizacionInventario(disponibles)));
        }
        catch (Exception e)
        {
            Avisar("No se pudo difundir la actualización de inventario: " + e.Message);
        }
    }

    private async Task EnviarSinFallarAsync(string sedeDestino, ContenidoMensaje contenido)
    {
        try
        {
            await bus.EnviarAsync(Sobre.ParaSede(codigoSede, sedeDestino, contenido));
        }
        catch (Exception e)
        {
            Avisar("No se pudo enviar " + contenido.Tipo + " a " + sedeDestino + ": " + e.Message);
        }
    }

    private void Avisar(string texto)
    {
        registrar("[Pedidos " + codigoSede + "] " + texto);
    }

    // ---------- Esperas activas (arreglo que crece) ----------

    private void AgregarEspera(EsperaReservas espera)
    {
        if (cantidadEsperas == esperas.Length)
        {
            var mayor = new EsperaReservas?[esperas.Length * 2];
            Array.Copy(esperas, mayor, cantidadEsperas);
            esperas = mayor;
        }
        esperas[cantidadEsperas++] = espera;
    }

    private EsperaReservas? BuscarEspera(string idPedido)
    {
        for (int i = 0; i < cantidadEsperas; i++)
        {
            if (esperas[i]!.Pedido.Id == idPedido)
                return esperas[i];
        }
        return null;
    }

    private void QuitarEspera(EsperaReservas espera)
    {
        for (int i = 0; i < cantidadEsperas; i++)
        {
            if (esperas[i] == espera)
            {
                // Se mueve el último a este hueco: el orden no importa.
                esperas[i] = esperas[cantidadEsperas - 1];
                esperas[cantidadEsperas - 1] = null;
                cantidadEsperas--;
                return;
            }
        }
    }

    // Estado de un pedido mientras llegan las respuestas de sus sedes remotas.
    private class EsperaReservas
    {
        public Pedido Pedido { get; }
        public LineaReserva[] Locales { get; }
        public string[] Remotas { get; }

        // Se completa cuando ya no hace falta esperar: todas aceptaron o alguna rechazó.
        public TaskCompletionSource<bool> Listo { get; } =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? MotivoRechazo { get; private set; }

        private readonly bool[] respondio;
        private readonly bool[] rechazo;
        private int aceptadas;

        public EsperaReservas(Pedido pedido, LineaReserva[] locales, string[] remotas)
        {
            Pedido = pedido;
            Locales = locales;
            Remotas = remotas;
            respondio = new bool[remotas.Length];
            rechazo = new bool[remotas.Length];
        }

        public bool TodasAceptadas => MotivoRechazo == null && aceptadas == Remotas.Length;

        public bool Rechazo(int indice) => rechazo[indice];

        // Anota la respuesta de una sede. Ignora sedes que no participan y respuestas repetidas.
        public void Registrar(string sede, bool aceptada, string motivo)
        {
            int i = Array.IndexOf(Remotas, sede);
            if (i < 0 || respondio[i])
                return;
            respondio[i] = true;

            if (aceptada)
            {
                aceptadas++;
                if (aceptadas == Remotas.Length)
                    Listo.TrySetResult(true);
            }
            else
            {
                rechazo[i] = true;
                if (MotivoRechazo == null)
                    MotivoRechazo = sede + " no pudo reservar: " + motivo;
                Listo.TrySetResult(false);
            }
        }

        // Para cuando ni siquiera se pudo mandar la solicitud.
        public void Fallar(string motivo)
        {
            if (MotivoRechazo == null)
                MotivoRechazo = motivo;
            Listo.TrySetResult(false);
        }

        // "SJO, ALA": las sedes que todavía no contestaron.
        public string SinResponder()
        {
            string texto = "";
            for (int i = 0; i < Remotas.Length; i++)
            {
                if (!respondio[i])
                    texto += (texto.Length > 0 ? ", " : "") + Remotas[i];
            }
            return texto;
        }
    }
}
