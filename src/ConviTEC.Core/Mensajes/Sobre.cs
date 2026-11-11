using System;

namespace ConviTEC.Core.Mensajes;

// Sobre común de todos los mensajes entre sedes (documento de diseño, sección 5.7).
// Lleva los datos que todo mensaje necesita (quién lo envía, a quién, cuándo y un
// identificador único) y adentro el contenido propio de cada tipo de mensaje.
public class Sobre
{
    // Identificador único: código de la sede de origen + GUID (por ejemplo "LIB-3f9a2c7e...").
    // La sede que recibe lo usa para no procesar dos veces el mismo mensaje.
    public string IdMensaje { get; }

    // Tipo del mensaje. Se toma del contenido, así nunca se contradicen.
    public TipoMensaje Tipo => Contenido.Tipo;

    // Código de la sede que envía, por ejemplo "LIB".
    public string SedeOrigen { get; }

    // Código de la sede destino, por ejemplo "SJO".
    // Es null cuando el mensaje es una difusión a todas las sedes.
    public string? SedeDestino { get; }

    // Fecha y hora en que se creó el mensaje, con zona horaria.
    public DateTimeOffset FechaHora { get; }

    // Datos propios del tipo de mensaje.
    public ContenidoMensaje Contenido { get; }

    // True si el mensaje va a todas las sedes (no tiene destino).
    public bool EsDifusion => SedeDestino == null;

    // Arma un sobre con todos sus datos. Lo usa la mensajería al recibir un mensaje,
    // porque el id y la fecha ya vienen del emisor. Para enviar uno nuevo se usan
    // ParaSede o ParaTodas.
    public Sobre(string idMensaje, string sedeOrigen, string? sedeDestino,
                 DateTimeOffset fechaHora, ContenidoMensaje contenido)
    {
        if (string.IsNullOrWhiteSpace(idMensaje))
            throw new ArgumentException("El mensaje debe tener un identificador.", nameof(idMensaje));
        if (string.IsNullOrWhiteSpace(sedeOrigen))
            throw new ArgumentException("El mensaje debe indicar la sede de origen.", nameof(sedeOrigen));
        if (sedeDestino != null && sedeDestino.Trim().Length == 0)
            throw new ArgumentException("La sede destino no puede estar vacía; use null para una difusión.", nameof(sedeDestino));
        if (contenido == null)
            throw new ArgumentNullException(nameof(contenido), "El mensaje debe tener contenido.");

        IdMensaje = idMensaje;
        SedeOrigen = sedeOrigen;
        SedeDestino = sedeDestino;
        FechaHora = fechaHora;
        Contenido = contenido;
    }

    // Crea un mensaje nuevo dirigido a una sola sede (exchange directo).
    public static Sobre ParaSede(string sedeOrigen, string sedeDestino, ContenidoMensaje contenido)
    {
        if (string.IsNullOrWhiteSpace(sedeDestino))
            throw new ArgumentException("Un mensaje directo necesita una sede destino.", nameof(sedeDestino));
        return new Sobre(NuevoId(sedeOrigen), sedeOrigen, sedeDestino, DateTimeOffset.Now, contenido);
    }

    // Crea un mensaje nuevo para todas las sedes (exchange de difusión).
    public static Sobre ParaTodas(string sedeOrigen, ContenidoMensaje contenido)
    {
        return new Sobre(NuevoId(sedeOrigen), sedeOrigen, null, DateTimeOffset.Now, contenido);
    }

    // Genera un id único: código de sede + GUID sin guiones.
    private static string NuevoId(string sedeOrigen)
    {
        return sedeOrigen + "-" + Guid.NewGuid().ToString("N");
    }
}
