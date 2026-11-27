using System;
using System.Threading.Tasks;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;

namespace ConviTEC.Core.Servicios;

// Mantiene al día la copia del inventario global de esta sede (diseño 5.7, 5.10 y RF-05).
//
// Tres mensajes:
//   ActualizacionInventario (difusión)  otra sede avisa cuánto le queda de algunos productos
//                                       → se cambian solo esos productos en la copia.
//   SolicitudSincronizacion (difusión)  otra sede (recién encendida) pide el inventario de todas
//                                       → se le contesta con EstadoInventario, directo a ella.
//   EstadoInventario (directo)          otra sede manda su inventario completo
//                                       → se reemplaza todo lo que se sabía de ella.
//
// Al arrancar, la sede llama PedirSincronizacionAsync (5.10, paso 5) para refrescar una copia que
// pudo quedar vieja mientras estuvo apagada.
//
// Por qué no se pisa información nueva con vieja: cada sede publica todo por un mismo canal, y
// RabbitMQ entrega en orden lo que un mismo canal publicó en una misma cola. Si SJO manda su
// EstadoInventario y después una ActualizacionInventario, aquí llegan en ese orden.
// Además, las actualizaciones traen la cantidad final (no una diferencia): aplicarlas dos veces
// deja el mismo resultado (RNF-10).
public class ServicioSincronizacion
{
    private readonly string codigoSede;
    private readonly IInventarioReservas local;
    private readonly IInventarioGlobal global;
    private readonly IBusMensajes bus;
    private readonly Action<string> registrar;

    public ServicioSincronizacion(string codigoSede, IInventarioReservas local, IInventarioGlobal global,
                                  IBusMensajes bus, Action<string>? registrar = null)
    {
        if (string.IsNullOrWhiteSpace(codigoSede))
            throw new ArgumentException("Falta el código de la sede.", nameof(codigoSede));

        this.codigoSede = codigoSede;
        this.local = local ?? throw new ArgumentNullException(nameof(local));
        this.global = global ?? throw new ArgumentNullException(nameof(global));
        this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
        this.registrar = registrar ?? Console.WriteLine;
    }

    // Le dice al despachador qué mensajes atiende este servicio.
    public void RegistrarEn(Despachador despachador)
    {
        if (despachador == null)
            throw new ArgumentNullException(nameof(despachador));
        despachador.Registrar(TipoMensaje.ActualizacionInventario, AlRecibirActualizacionAsync);
        despachador.Registrar(TipoMensaje.SolicitudSincronizacion, AlRecibirSolicitudAsync);
        despachador.Registrar(TipoMensaje.EstadoInventario, AlRecibirEstadoAsync);
    }

    // ---------- Lo que manda esta sede ----------

    // Pide a todas las demás su inventario completo. Se llama al arrancar, después de conectar el bus.
    // Devuelve false si no se pudo (por ejemplo, sin RabbitMQ): la sede sigue con la copia que tenía.
    public async Task<bool> PedirSincronizacionAsync()
    {
        try
        {
            await bus.EnviarAsync(Sobre.ParaTodas(codigoSede, new SolicitudSincronizacion()));
            Avisar("Se pidió el inventario a las demás sedes.");
            return true;
        }
        catch (Exception e)
        {
            Avisar("No se pudo pedir la sincronización: " + e.Message);
            return false;
        }
    }

    // Avisa a las demás cuánto queda disponible de estos productos. Para cambios que no pasan por
    // una reserva, por ejemplo incorporar unidades nuevas al inventario (RF-03, rol A).
    // Devuelve false si no se pudo enviar (el cambio local ya quedó hecho igual).
    public async Task<bool> DifundirCambiosAsync(string[] codigosProducto)
    {
        if (codigosProducto == null || codigosProducto.Length == 0)
            return true;
        try
        {
            var lineas = new LineaMensaje[codigosProducto.Length];
            for (int i = 0; i < codigosProducto.Length; i++)
                lineas[i] = new LineaMensaje(codigosProducto[i], local.Disponible(codigosProducto[i]));
            await bus.EnviarAsync(Sobre.ParaTodas(codigoSede, new ActualizacionInventario(lineas)));
            return true;
        }
        catch (Exception e)
        {
            Avisar("No se pudo difundir el cambio de inventario: " + e.Message);
            return false;
        }
    }

    // ---------- Lo que llega de las demás ----------

    public Task AlRecibirActualizacionAsync(Sobre sobre)
    {
        string sede = sobre.SedeOrigen;
        if (!EsOtraSedeConocida(sede, "ActualizacionInventario"))
            return Task.CompletedTask;

        LineaMensaje[] lineas = LineasValidas(((ActualizacionInventario)sobre.Contenido).Lineas, sede);
        if (lineas.Length > 0)
            global.ActualizarSede(sede, lineas);
        return Task.CompletedTask;
    }

    public async Task AlRecibirSolicitudAsync(Sobre sobre)
    {
        string sede = sobre.SedeOrigen;
        if (!EsOtraSedeConocida(sede, "SolicitudSincronizacion"))
            return;

        // Se manda lo DISPONIBLE (cantidad - reservado): es lo único que otra sede puede pedir.
        LineaInventario[] existencias = local.Existencias();
        var lineas = new LineaMensaje[existencias.Length];
        for (int i = 0; i < existencias.Length; i++)
            lineas[i] = new LineaMensaje(existencias[i].Codigo, existencias[i].Cantidad - existencias[i].Reservado);

        // Si el envío falla, la excepción devuelve la solicitud a la cola y se reintenta.
        await bus.EnviarAsync(Sobre.ParaSede(codigoSede, sede, new EstadoInventario(lineas)));
        Avisar("Se le mandó el inventario a " + sede + " (" + lineas.Length + " productos).");
    }

    public Task AlRecibirEstadoAsync(Sobre sobre)
    {
        string sede = sobre.SedeOrigen;
        if (!EsOtraSedeConocida(sede, "EstadoInventario"))
            return Task.CompletedTask;

        LineaMensaje[] lineas = LineasValidas(((EstadoInventario)sobre.Contenido).Lineas, sede);
        global.ReemplazarSede(sede, lineas);
        Avisar("Copia del inventario de " + sede + " actualizada (" + lineas.Length + " productos).");
        return Task.CompletedTask;
    }

    // ---------- Ayudantes ----------

    // Solo se aceptan sedes de la configuración, y nunca esta misma (no tiene copia de sí misma).
    // Una sede desconocida (por ejemplo las de prueba PRA/PRB) se ignora para no ensuciar la copia,
    // que después decide a quién se le piden productos.
    private bool EsOtraSedeConocida(string sede, string tipo)
    {
        if (sede == codigoSede)
            return false;

        string[] sedes = global.Sedes();
        for (int i = 0; i < sedes.Length; i++)
        {
            if (sedes[i] == sede)
                return true;
        }
        Avisar(tipo + " de una sede desconocida (" + sede + "); se ignora.");
        return false;
    }

    // Quita las líneas sin código o con cantidad negativa (no se puede tener -3 disponibles).
    private LineaMensaje[] LineasValidas(LineaMensaje[]? lineas, string sede)
    {
        if (lineas == null)
            return new LineaMensaje[0];

        var temporal = new LineaMensaje[lineas.Length];
        int cantidad = 0;
        for (int i = 0; i < lineas.Length; i++)
        {
            if (lineas[i] == null || string.IsNullOrWhiteSpace(lineas[i].Codigo) || lineas[i].Cantidad < 0)
            {
                Avisar("Línea inválida en el inventario de " + sede + "; se ignora.");
                continue;
            }
            temporal[cantidad++] = lineas[i];
        }

        var resultado = new LineaMensaje[cantidad];
        Array.Copy(temporal, resultado, cantidad);
        return resultado;
    }

    private void Avisar(string texto)
    {
        registrar("[Sincronización " + codigoSede + "] " + texto);
    }
}
