namespace ConviTEC.Core.Mensajes;

/// <summary>
/// Una línea dentro de un mensaje: un producto y una cantidad.
/// Se usa en SolicitudReserva (cantidad a reservar), ActualizacionInventario y
/// EstadoInventario (cantidad disponible).
/// </summary>
public class LineaMensaje
{
    /// <summary>Código del producto, por ejemplo "P040".</summary>
    public string Codigo { get; set; } = "";

    /// <summary>Cantidad de unidades. Su significado depende del mensaje que contiene la línea.</summary>
    public int Cantidad { get; set; }

    /// <summary>Constructor vacío: lo necesita la deserialización desde JSON.</summary>
    public LineaMensaje() { }

    public LineaMensaje(string codigo, int cantidad)
    {
        Codigo = codigo;
        Cantidad = cantidad;
    }
}
