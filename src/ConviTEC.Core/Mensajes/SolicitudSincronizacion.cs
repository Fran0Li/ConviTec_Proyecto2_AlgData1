namespace ConviTEC.Core.Mensajes;

// Una sede que acaba de arrancar le pide a las demás su inventario completo para
// refrescar su copia del inventario global (diseño 5.10, paso 5).
// Va por el exchange de difusión. Cada sede que la recibe responde con EstadoInventario
// directo a quien la pidió (Sobre.SedeOrigen).
// No lleva datos: el tipo y la sede de origen del sobre ya dicen todo.
public class SolicitudSincronizacion : ContenidoMensaje
{
    public override TipoMensaje Tipo => TipoMensaje.SolicitudSincronizacion;
}
