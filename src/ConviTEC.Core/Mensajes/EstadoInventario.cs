namespace ConviTEC.Core.Mensajes;

// Respuesta a una SolicitudSincronizacion: el inventario completo de una sede.
// Va por el exchange directo a la sede que lo pidió, que reemplaza con esto la
// copia de esa sede en su inventario global.
// La sede a la que pertenece el inventario es la que envía (Sobre.SedeOrigen).
public class EstadoInventario : ContenidoMensaje
{
    public override TipoMensaje Tipo => TipoMensaje.EstadoInventario;

    // Todos los productos de la sede con su cantidad disponible.
    public LineaMensaje[] Lineas { get; set; } = new LineaMensaje[0];

    public EstadoInventario() { }

    public EstadoInventario(LineaMensaje[] lineas)
    {
        Lineas = lineas;
    }
}
