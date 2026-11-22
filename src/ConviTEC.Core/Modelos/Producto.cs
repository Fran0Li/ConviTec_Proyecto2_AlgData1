using System;

namespace ConviTEC.Core.Modelos;

// Un producto del catalogo de ConviTEC.
// El codigo es unico en toda la cadena: el mismo producto tiene el mismo codigo en todas las sedes.
public class Producto
{
    // Codigo unico, por ejemplo "P040".
    public string Codigo { get; set; } = "";

    public string Nombre { get; set; } = "";

    public string Descripcion { get; set; } = "";

    // Categoría, por ejemplo "Bebidas" o "Snacks".
    public string Categoria { get; set; } = "";

    // Precio de venta en colones.
    public decimal Precio { get; set; }

    // Constructor vacío: lo necesita la lectura desde JSON.
    public Producto() { }

    public Producto(string codigo, string nombre, string descripcion, string categoria, decimal precio)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El producto debe tener un código.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El producto debe tener un nombre.", nameof(nombre));
        if (precio < 0)
            throw new ArgumentException("El precio no puede ser negativo.", nameof(precio));

        Codigo = codigo.Trim();
        Nombre = nombre.Trim();
        Descripcion = descripcion ?? "";
        Categoria = categoria ?? "";
        Precio = precio;
    }
}