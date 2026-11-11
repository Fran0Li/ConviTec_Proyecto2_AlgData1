namespace ConviTEC.Core.Mensajes;

// Una línea dentro de un mensaje: un producto y una cantidad.
// Se usa en SolicitudReserva (cantidad a reservar), ActualizacionInventario y
// EstadoInventario (cantidad disponible).
public class LineaMensaje
{
    // Código del producto, por ejemplo "P040".
    public string Codigo { get; set; } = "";

    // Cantidad de unidades. Su significado depende del mensaje que contiene la línea.
    public int Cantidad { get; set; }

    // Constructor vacío: lo necesita la deserialización desde JSON.
    public LineaMensaje() { }

    public LineaMensaje(string codigo, int cantidad)
    {
        Codigo = codigo;
        Cantidad = cantidad;
    }
}
