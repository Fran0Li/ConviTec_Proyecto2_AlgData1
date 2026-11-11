namespace ConviTEC.Core.Mensajes;

// La sede receptora de un pedido le pide a otra sede que le aparte productos.
// Va por el exchange directo a la sede dueña de los productos.
// Quien la recibe reserva todas las líneas o ninguna (todo o nada) y responde con RespuestaReserva.
public class SolicitudReserva : ContenidoMensaje
{
    public override TipoMensaje Tipo => TipoMensaje.SolicitudReserva;

    // Identifica esta reserva en las dos sedes, por ejemplo "LIB-0042-SJO".
    // Sirve para confirmar o liberar después y para no reservar dos veces lo mismo.
    public string IdReserva { get; set; } = "";

    // Pedido al que pertenece la reserva, por ejemplo "LIB-0042".
    public string IdPedido { get; set; } = "";

    // Productos y cantidades a reservar.
    public LineaMensaje[] Lineas { get; set; } = new LineaMensaje[0];

    public SolicitudReserva() { }

    public SolicitudReserva(string idReserva, string idPedido, LineaMensaje[] lineas)
    {
        IdReserva = idReserva;
        IdPedido = idPedido;
        Lineas = lineas;
    }
}
