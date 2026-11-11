namespace ConviTEC.Core.Mensajes;

// Aviso de que cambió el inventario de una sede (por una confirmación o por
// incorporar unidades). Va por el exchange de difusión a todas las sedes, que
// actualizan su copia del inventario global.
// La sede a la que pertenece el inventario es la que envía (Sobre.SedeOrigen).
public class ActualizacionInventario : ContenidoMensaje
{
    public override TipoMensaje Tipo => TipoMensaje.ActualizacionInventario;

    // Solo los productos que cambiaron, con su cantidad disponible FINAL (no la diferencia).
    // Así, si el mensaje llega repetido, aplicarlo otra vez deja el mismo resultado.
    public LineaMensaje[] Lineas { get; set; } = new LineaMensaje[0];

    public ActualizacionInventario() { }

    public ActualizacionInventario(LineaMensaje[] lineas)
    {
        Lineas = lineas;
    }
}
