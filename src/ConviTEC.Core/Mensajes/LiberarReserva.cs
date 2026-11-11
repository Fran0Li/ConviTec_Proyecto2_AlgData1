namespace ConviTEC.Core.Mensajes;

// Segunda fase cuando el pedido se canceló: la sede dueña debe devolver las
// unidades reservadas a disponible.
// Va por el exchange directo a la sede dueña.
public class LiberarReserva : ContenidoMensaje
{
    public override TipoMensaje Tipo => TipoMensaje.LiberarReserva;

    public string IdReserva { get; set; } = "";

    // Por qué se libera, por ejemplo "Otra sede no tenía existencias" o
    // "Tiempo límite agotado". Sirve para el registro de la sede dueña.
    public string Motivo { get; set; } = "";

    public LiberarReserva() { }

    public LiberarReserva(string idReserva, string motivo)
    {
        IdReserva = idReserva;
        Motivo = motivo;
    }
}
