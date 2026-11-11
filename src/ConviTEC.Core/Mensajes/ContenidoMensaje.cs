namespace ConviTEC.Core.Mensajes;

// Clase base de los 7 contenidos posibles de un mensaje.
// Cada tipo de mensaje es una clase hija que define sus propios datos
// (por ejemplo, SolicitudReserva tiene idReserva, idPedido y líneas).
public abstract class ContenidoMensaje
{
    // Tipo de mensaje que representa este contenido. Cada clase hija lo fija,
    // así el tipo del sobre nunca puede contradecir a su contenido.
    public abstract TipoMensaje Tipo { get; }
}
