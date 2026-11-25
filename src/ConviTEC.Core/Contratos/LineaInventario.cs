namespace ConviTEC.Core.Contratos;

// Una fila de inventario.json: las existencias de un producto en esta sede (diseño 5.4 y 5.10).
// Lo que se puede ofrecer a un pedido nuevo es Cantidad - Reservado.
// Es un objeto de datos para leer y escribir el archivo; con estas filas el rol A arma su árbol AVL.
public class LineaInventario
{
    // Código del producto, por ejemplo "P040".
    public string Codigo { get; set; } = "";

    // Unidades físicas en la sede.
    public int Cantidad { get; set; }

    // Unidades apartadas para pedidos en proceso.
    public int Reservado { get; set; }

    // Constructor vacío: lo necesita la lectura desde JSON.
    public LineaInventario() { }

    public LineaInventario(string codigo, int cantidad, int reservado)
    {
        Codigo = codigo;
        Cantidad = cantidad;
        Reservado = reservado;
    }
}