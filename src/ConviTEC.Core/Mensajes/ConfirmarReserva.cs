namespace ConviTEC.Core.Mensajes;

// Segunda fase cuando todo salió bien: el pedido se confirmó y la sede dueña
// debe descontar de verdad las unidades reservadas.
// Va por el exchange directo a la sede dueña. Quien la recibe descuenta cantidad
// y reservado, y difunde una ActualizacionInventario.
public class ConfirmarReserva : ContenidoMensaje
{
    public override TipoMensaje Tipo => TipoMensaje.ConfirmarReserva;

    // Reserva que se confirma. Con eso basta: la sede dueña ya sabe qué productos tenía apartados.
    public string IdReserva { get; set; } = "";

    public ConfirmarReserva() { }

    public ConfirmarReserva(string idReserva)
    {
        IdReserva = idReserva;
    }
}
