using System;

namespace ConviTEC.Core.Modelos;


public class DetallePedido
{
    public string CodigoProducto { get; set; } = "";

    // Se guarda el nombre para poder mostrar el historial sin buscar el producto.
    public string NombreProducto { get; set; } = "";

    public int Cantidad { get; set; }

    // Precio al momento del pedido. Si despues cambia el precio del producto,
    // el historial conserva el precio con que se vendio.
    public decimal PrecioUnitario { get; set; }

    // Codigo de la sede que suministra este producto, por ejemplo "SJO".
    // Puede ser distinta de la sede donde se registro el pedido.
    public string CodigoSedeOrigen { get; set; } = "";

    // Cantidad por precio unitario.
    public decimal Subtotal => Cantidad * PrecioUnitario;

    public DetallePedido() { }

    public DetallePedido(string codigoProducto, string nombreProducto, int cantidad,
                         decimal precioUnitario, string codigoSedeOrigen)
    {
        if (string.IsNullOrWhiteSpace(codigoProducto))
            throw new ArgumentException("La linea debe indicar el producto.", nameof(codigoProducto));
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser mayor que cero.", nameof(cantidad));
        if (precioUnitario < 0)
            throw new ArgumentException("El precio no puede ser negativo.", nameof(precioUnitario));
        if (string.IsNullOrWhiteSpace(codigoSedeOrigen))
            throw new ArgumentException("La linea debe indicar la sede que suministra el producto.", nameof(codigoSedeOrigen));

        CodigoProducto = codigoProducto.Trim();
        NombreProducto = nombreProducto ?? "";
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
        CodigoSedeOrigen = codigoSedeOrigen.Trim();
    }
}