using System;
using System.Threading.Tasks;
using ConviTEC.Core.Mensajes;

namespace ConviTEC.Core.Servicios;

// Recibe cada mensaje que llega del bus y se lo pasa al servicio que lo atiende (diseño 5.7).
// Antes de entregarlo descarta lo que la sede no debe procesar:
//   1. sus propias difusiones (el fanout también se las manda a ella misma),
//   2. mensajes directos dirigidos a otra sede (no debería pasar, pero no se procesan),
//   3. mensajes repetidos (mismo idMensaje que uno ya procesado).
//
// Uso:
//   var despachador = new Despachador("LIB");
//   despachador.Registrar(TipoMensaje.SolicitudReserva, servicioReservas.AlRecibirSolicitudAsync);
//   await bus.IniciarAsync(despachador.ProcesarAsync);
//
// No necesita candados: el bus entrega los mensajes de uno en uno (prefetch = 1).
public class Despachador
{
    public const int CapacidadPorDefecto = 1000;

    private readonly string codigoSede;

    // Una casilla por tipo de mensaje, indexada por el valor del enum:
    // manejadores[(int)TipoMensaje.SolicitudReserva] es quien atiende las solicitudes de reserva.
    // Una casilla en null significa que nadie atiende ese tipo.
    private readonly Func<Sobre, Task>?[] manejadores;

    private readonly MemoriaIds procesados;
    private readonly Action<string> registrar;

    public Despachador(string codigoSede, int capacidadMemoria = CapacidadPorDefecto, Action<string>? registrar = null)
    {
        if (string.IsNullOrWhiteSpace(codigoSede))
            throw new ArgumentException("Falta el código de la sede.", nameof(codigoSede));

        this.codigoSede = codigoSede;
        manejadores = new Func<Sobre, Task>?[Enum.GetValues<TipoMensaje>().Length];
        procesados = new MemoriaIds(capacidadMemoria);
        this.registrar = registrar ?? Console.WriteLine;
    }

    // Indica qué método atiende un tipo de mensaje. Cada tipo tiene un solo manejador.
    public void Registrar(TipoMensaje tipo, Func<Sobre, Task> manejador)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), "Tipo de mensaje desconocido: " + tipo + ".");
        if (manejador == null)
            throw new ArgumentNullException(nameof(manejador));
        if (manejadores[(int)tipo] != null)
            throw new InvalidOperationException("Ya hay un manejador registrado para " + tipo + ".");

        manejadores[(int)tipo] = manejador;
    }

    // Se le pasa al bus como alRecibir. Si el manejador lanza una excepción, la excepción sigue
    // hacia el bus, que devuelve el mensaje a la cola para intentarlo de nuevo.
    public async Task ProcesarAsync(Sobre sobre)
    {
        if (sobre == null)
            throw new ArgumentNullException(nameof(sobre));

        // 1. Difusión que mandó esta misma sede: ya aplicó el cambio localmente al enviarla.
        if (sobre.EsDifusion && MismaSede(sobre.SedeOrigen))
            return;

        // 2. Directo para otra sede: el exchange directo no debería entregarlo aquí.
        if (!sobre.EsDifusion && !MismaSede(sobre.SedeDestino!))
        {
            Avisar("Mensaje " + sobre.IdMensaje + " es para " + sobre.SedeDestino + "; se ignora.");
            return;
        }

        // 3. Ya procesado antes: RabbitMQ lo repitió.
        if (procesados.Contiene(sobre.IdMensaje))
        {
            Avisar("Mensaje repetido " + sobre.IdMensaje + " (" + sobre.Tipo + "); se ignora.");
            return;
        }

        Func<Sobre, Task>? manejador = manejadores[(int)sobre.Tipo];
        if (manejador == null)
        {
            // Se ignora en vez de lanzar error: si volviera a la cola rebotaría para siempre.
            Avisar("Nadie atiende mensajes de tipo " + sobre.Tipo + "; se ignora " + sobre.IdMensaje + ".");
            return;
        }

        await manejador(sobre);

        // Se marca como procesado DESPUÉS de que el manejador terminó bien. Si se marcara antes y el
        // manejador fallara, el reintento que manda RabbitMQ se descartaría como "repetido" y el
        // mensaje se perdería.
        procesados.Agregar(sobre.IdMensaje);
    }

    private bool MismaSede(string codigo)
    {
        return string.Equals(codigo, codigoSede, StringComparison.Ordinal);
    }

    private void Avisar(string texto)
    {
        registrar("[Despachador " + codigoSede + "] " + texto);
    }
}
