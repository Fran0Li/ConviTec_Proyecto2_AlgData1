namespace ConviTEC.Core.Mensajes;

// Respuesta de la sede dueña a una SolicitudReserva.
// Va por el exchange directo a la sede que pidió la reserva.
// Si llega cuando el pedido ya se canceló (por tiempo límite), quien la recibe
// responde con LiberarReserva para que no queden unidades apartadas (D-03).
public class RespuestaReserva : ContenidoMensaje
{
    public override TipoMensaje Tipo => TipoMensaje.RespuestaReserva;

    public string IdReserva { get; set; } = "";

    public string IdPedido { get; set; } = "";

    // True si se reservaron todas las líneas; false si no se reservó nada.
    public bool Aceptada { get; set; }

    // Razón del rechazo para mostrársela al dependiente, por ejemplo
    // "No hay suficientes unidades de P040". Vacío si se aceptó.
    public string Motivo { get; set; } = "";

    public RespuestaReserva() { }

    public RespuestaReserva(string idReserva, string idPedido, bool aceptada, string motivo)
    {
        IdReserva = idReserva;
        IdPedido = idPedido;
        Aceptada = aceptada;
        Motivo = motivo;
    }
}
