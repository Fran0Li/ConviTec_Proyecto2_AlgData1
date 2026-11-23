using System;
using System.Threading;
using System.Threading.Tasks;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Servicios;

// Reservas en dos fases, del lado de la SEDE DUEÑA de los productos (diseño 5.6, 5.7 y D-03).
//
//   Sede que pide                         Sede dueña (este servicio)
//   SolicitudReserva  ───────────────►    aparta todo o nada, guarda la reserva
//                     ◄───────────────    RespuestaReserva (aceptada o no, y por qué)
//   ConfirmarReserva  ───────────────►    descuenta del inventario y difunde ActualizacionInventario
//     o LiberarReserva ──────────────►    devuelve las unidades a disponible
//
// Si nunca llega ni la confirmación ni la liberación (la sede que pide se cayó), la reserva vence a
// los 5 minutos y se libera sola (LiberarVencidas), para no dejar unidades apartadas para siempre.
//
// Idempotencia por idReserva: el despachador ya ignora un mismo idMensaje repetido, pero la misma
// reserva puede llegar en otro mensaje (o tras un reinicio, cuando el despachador ya olvidó los ids).
// Por eso cada operación revisa primero el estado guardado de la reserva:
//   - SolicitudReserva repetida → no aparta otra vez; reenvía la respuesta.
//   - ConfirmarReserva o LiberarReserva repetidas → no hacen nada.
//
// El lado de la sede que pide (mandar solicitudes, esperar 10 s, cancelar) va en el registro de pedidos.
public class ServicioReservas
{
    // Tiempo que una reserva puede quedar pendiente antes de liberarse sola (diseño 5.7).
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(5);

    private readonly string codigoSede;
    private readonly IInventarioReservas inventario;
    private readonly IRegistroReservas registro;
    private readonly IBusMensajes bus;
    private readonly Func<DateTime> reloj;
    private readonly Action<string> registrar;

    // Los mensajes llegan de uno en uno, pero LiberarVencidas corre en otro hilo (un temporizador).
    // Este candado hace que nunca se trabaje sobre la misma reserva desde dos lados a la vez.
    private readonly SemaphoreSlim candado = new SemaphoreSlim(1, 1);

    // reloj: de dónde sale la hora actual. En las pruebas se pasa uno que se puede adelantar,
    // así se prueba el vencimiento sin esperar 5 minutos de verdad.
    public ServicioReservas(string codigoSede, IInventarioReservas inventario, IRegistroReservas registro,
                            IBusMensajes bus, Func<DateTime>? reloj = null, Action<string>? registrar = null)
    {
        if (string.IsNullOrWhiteSpace(codigoSede))
            throw new ArgumentException("Falta el código de la sede.", nameof(codigoSede));

        this.codigoSede = codigoSede;
        this.inventario = inventario ?? throw new ArgumentNullException(nameof(inventario));
        this.registro = registro ?? throw new ArgumentNullException(nameof(registro));
        this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        this.reloj = reloj ?? (() => DateTime.Now);
        this.registrar = registrar ?? Console.WriteLine;
    }

    // Le dice al despachador qué mensajes atiende este servicio.
    public void RegistrarEn(Despachador despachador)
    {
        if (despachador == null)
            throw new ArgumentNullException(nameof(despachador));
        despachador.Registrar(TipoMensaje.SolicitudReserva, AlRecibirSolicitudAsync);
        despachador.Registrar(TipoMensaje.ConfirmarReserva, AlRecibirConfirmacionAsync);
        despachador.Registrar(TipoMensaje.LiberarReserva, AlRecibirLiberacionAsync);
    }

    // ---------- Fase 1: SolicitudReserva ----------

    public async Task AlRecibirSolicitudAsync(Sobre sobre)
    {
        var solicitud = (SolicitudReserva)sobre.Contenido;
        string solicitante = sobre.SedeOrigen;

        await candado.WaitAsync();
        RespuestaReserva respuesta;
        try
        {
            respuesta = Atender(solicitud, solicitante);
        }
        finally
        {
            candado.Release();
        }

        // Se responde fuera del candado. Si el envío falla, la excepción devuelve la solicitud a la
        // cola; al reintentar, la reserva ya está guardada y solo se reenvía la respuesta.
        await bus.EnviarAsync(Sobre.ParaSede(codigoSede, solicitante, respuesta));
    }

    private RespuestaReserva Atender(SolicitudReserva solicitud, string solicitante)
    {
        string id = solicitud.IdReserva;

        // Repetida: no se aparta otra vez, se contesta según cómo quedó la primera.
        Reserva? existente = registro.Buscar(id);
        if (existente != null)
        {
            Avisar("Solicitud repetida de " + id + " (" + existente.Estado + "); se reenvía la respuesta.");
            return RespuestaSegunEstado(existente, solicitud.IdPedido);
        }

        if (string.IsNullOrWhiteSpace(id))
            return Rechazo(solicitud, "La solicitud no tiene identificador de reserva.");

        LineaReserva[]? lineas = ConvertirLineas(solicitud.Lineas, out string error);
        if (lineas == null)
            return Rechazo(solicitud, error);

        // El inventario aparta todo o nada.
        bool aceptada = inventario.Reservar(lineas, out string motivo);

        var reserva = new Reserva(id, solicitud.IdPedido, solicitante, codigoSede, lineas, reloj(), Duracion);
        if (!aceptada)
            reserva.Estado = EstadoReserva.Rechazada;   // se guarda igual, para contestar lo mismo si se repite

        try
        {
            registro.Guardar(reserva);
        }
        catch
        {
            // Si no se pudo guardar, se deshace el apartado. Si no, al reintentar la solicitud no
            // encontraría la reserva y apartaría las unidades por segunda vez.
            if (aceptada)
                inventario.Liberar(lineas);
            throw;
        }

        Avisar("Reserva " + id + " para " + solicitante + ": " + (aceptada ? "aceptada" : "rechazada (" + motivo + ")") + ".");
        return new RespuestaReserva(id, solicitud.IdPedido, aceptada, aceptada ? "" : motivo);
    }

    private static RespuestaReserva RespuestaSegunEstado(Reserva reserva, string idPedido)
    {
        switch (reserva.Estado)
        {
            case EstadoReserva.Pendiente:
            case EstadoReserva.Confirmada:
                return new RespuestaReserva(reserva.IdReserva, idPedido, true, "");
            case EstadoReserva.Liberada:
                // Ya no tiene unidades apartadas: contestar "aceptada" llevaría a confirmar algo que no existe.
                return new RespuestaReserva(reserva.IdReserva, idPedido, false, "La reserva ya fue liberada.");
            default:
                return new RespuestaReserva(reserva.IdReserva, idPedido, false, "La reserva ya había sido rechazada.");
        }
    }

    private RespuestaReserva Rechazo(SolicitudReserva solicitud, string motivo)
    {
        Avisar("Solicitud " + solicitud.IdReserva + " rechazada: " + motivo);
        return new RespuestaReserva(solicitud.IdReserva, solicitud.IdPedido, false, motivo);
    }

    // Pasa las líneas del mensaje al modelo. Devuelve null (y el motivo) si alguna no es válida,
    // por ejemplo cantidad cero o negativa, o si no hay ninguna.
    private static LineaReserva[]? ConvertirLineas(LineaMensaje[]? lineas, out string error)
    {
        if (lineas == null || lineas.Length == 0)
        {
            error = "La solicitud no tiene productos.";
            return null;
        }

        var resultado = new LineaReserva[lineas.Length];
        for (int i = 0; i < lineas.Length; i++)
        {
            if (lineas[i] == null || string.IsNullOrWhiteSpace(lineas[i].Codigo) || lineas[i].Cantidad <= 0)
            {
                error = "La línea " + (i + 1) + " de la solicitud no es válida.";
                return null;
            }
            resultado[i] = new LineaReserva(lineas[i].Codigo, lineas[i].Cantidad);
        }
        error = "";
        return resultado;
    }

    // ---------- Fase 2a: ConfirmarReserva ----------

    public async Task AlRecibirConfirmacionAsync(Sobre sobre)
    {
        string id = ((ConfirmarReserva)sobre.Contenido).IdReserva;
        Reserva? confirmada = null;

        await candado.WaitAsync();
        try
        {
            Reserva? reserva = BuscarParaFase2(id, "confirmar");
            if (reserva != null)
            {
                inventario.Confirmar(reserva.Lineas);
                reserva.Confirmar();
                registro.Guardar(reserva);
                confirmada = reserva;
                Avisar("Reserva " + id + " confirmada.");
            }
        }
        finally
        {
            candado.Release();
        }

        if (confirmada != null)
            await DifundirDisponiblesAsync(confirmada.Lineas);
    }

    // Avisa a las demás sedes cuántas unidades quedan disponibles de cada producto confirmado.
    // Va la cantidad final, no la diferencia: si el aviso llega dos veces, el resultado es el mismo.
    // Si falla el envío solo se anota: la confirmación ya quedó hecha, y las demás sedes se ponen al
    // día con el próximo aviso o con la sincronización al arrancar.
    private async Task DifundirDisponiblesAsync(LineaReserva[] lineas)
    {
        try
        {
            LineaMensaje[] disponibles = DisponiblesSinRepetir(lineas);
            await bus.EnviarAsync(Sobre.ParaTodas(codigoSede, new ActualizacionInventario(disponibles)));
        }
        catch (Exception e)
        {
            Avisar("No se pudo difundir la actualización de inventario: " + e.Message);
        }
    }

    // Una línea por producto (si el mismo producto venía en dos líneas, se avisa una sola vez).
    private LineaMensaje[] DisponiblesSinRepetir(LineaReserva[] lineas)
    {
        var temporal = new LineaMensaje[lineas.Length];
        int cantidad = 0;
        for (int i = 0; i < lineas.Length; i++)
        {
            string codigo = lineas[i].CodigoProducto;
            bool yaEsta = false;
            for (int j = 0; j < cantidad && !yaEsta; j++)
                yaEsta = temporal[j].Codigo == codigo;
            if (!yaEsta)
                temporal[cantidad++] = new LineaMensaje(codigo, inventario.Disponible(codigo));
        }

        var resultado = new LineaMensaje[cantidad];
        Array.Copy(temporal, resultado, cantidad);
        return resultado;
    }

    // ---------- Fase 2b: LiberarReserva ----------

    public async Task AlRecibirLiberacionAsync(Sobre sobre)
    {
        var liberar = (LiberarReserva)sobre.Contenido;

        await candado.WaitAsync();
        try
        {
            Reserva? reserva = BuscarParaFase2(liberar.IdReserva, "liberar");
            if (reserva != null)
                Liberar(reserva, "liberada (" + liberar.Motivo + ")");
        }
        finally
        {
            candado.Release();
        }
    }

    // Busca la reserva y revisa que siga pendiente. Si no, anota por qué y devuelve null.
    // Nunca lanza excepción por esto: si lo hiciera, el mensaje volvería a la cola y fallaría siempre igual.
    private Reserva? BuscarParaFase2(string idReserva, string accion)
    {
        Reserva? reserva = registro.Buscar(idReserva);
        if (reserva == null)
        {
            Avisar("No se puede " + accion + " " + idReserva + ": no existe; se ignora.");
            return null;
        }
        if (reserva.Estado != EstadoReserva.Pendiente)
        {
            // Lo normal es que sea un mensaje repetido (ya se confirmó o ya se liberó).
            Avisar("No se puede " + accion + " " + idReserva + ": está " + reserva.Estado + "; se ignora.");
            return null;
        }
        return reserva;
    }

    private void Liberar(Reserva reserva, string comoQuedo)
    {
        inventario.Liberar(reserva.Lineas);
        reserva.Liberar();
        registro.Guardar(reserva);
        Avisar("Reserva " + reserva.IdReserva + " " + comoQuedo + ".");
    }

    // ---------- Vencimiento ----------

    // Libera las reservas pendientes que ya pasaron sus 5 minutos. La sede la llama cada cierto
    // tiempo (por ejemplo, cada 30 s) y también al arrancar. Devuelve cuántas liberó.
    public int LiberarVencidas()
    {
        DateTime ahora = reloj();
        int liberadas = 0;

        candado.Wait();
        try
        {
            Reserva[] pendientes = registro.Pendientes();
            for (int i = 0; i < pendientes.Length; i++)
            {
                if (pendientes[i].EstaVencida(ahora))
                {
                    Liberar(pendientes[i], "vencida y liberada");
                    liberadas++;
                }
            }
        }
        finally
        {
            candado.Release();
        }
        return liberadas;
    }

    private void Avisar(string texto)
    {
        registrar("[Reservas " + codigoSede + "] " + texto);
    }
}
