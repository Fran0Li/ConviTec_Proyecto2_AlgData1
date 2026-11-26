using System;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Servicios;

// Lo que pide el dependiente en una línea: un producto y cuántas unidades.
// El nombre, el precio y de qué sede sale lo completa ServicioPedidos.
public class LineaPedido
{
    public string CodigoProducto { get; set; } = "";

    public int Cantidad { get; set; }

    public LineaPedido() { }

    public LineaPedido(string codigoProducto, int cantidad)
    {
        CodigoProducto = codigoProducto;
        Cantidad = cantidad;
    }
}

// Cómo terminó el intento de registrar un pedido. Siempre trae un mensaje para el dependiente.
public class ResultadoPedido
{
    // True si el pedido quedó Pendiente (reservas confirmadas e inventarios descontados).
    public bool Aceptado { get; }

    // Explicación para mostrar en pantalla: por qué se rechazó o se canceló, o la confirmación.
    public string Mensaje { get; }

    // El pedido, si llegó a crearse (aceptado o cancelado). Null si se rechazó antes de crearlo
    // (datos inválidos, sin ruta, sin existencias en la cadena).
    public Pedido? Pedido { get; }

    public ResultadoPedido(bool aceptado, string mensaje, Pedido? pedido)
    {
        Aceptado = aceptado;
        Mensaje = mensaje ?? "";
        Pedido = pedido;
    }

    public static ResultadoPedido Rechazo(string motivo)
    {
        return new ResultadoPedido(false, motivo, null);
    }
}
