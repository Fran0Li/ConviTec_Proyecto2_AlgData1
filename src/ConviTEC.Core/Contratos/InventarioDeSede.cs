using ConviTEC.Core.Mensajes;

namespace ConviTEC.Core.Contratos;

// Una entrada de inventario_global.json: la última copia conocida del inventario de OTRA sede
// (diseño 5.10). Se actualiza con los mensajes EstadoInventario y ActualizacionInventario.
public class InventarioDeSede
{
    // Sede dueña de este inventario, por ejemplo "SJO".
    public string CodigoSede { get; set; } = "";

    // Por producto, las unidades disponibles que esa sede informó.
    public LineaMensaje[] Lineas { get; set; } = new LineaMensaje[0];

    public InventarioDeSede() { }

    public InventarioDeSede(string codigoSede, LineaMensaje[] lineas)
    {
        CodigoSede = codigoSede;
        Lineas = lineas;
    }
}