using System;

namespace ConviTEC.Core.Modelos;


public class Venta
{
  
    public string Id { get; set; } = "";

    public string IdPedido { get; set; } = "";


    public string CodigoSede { get; set; } = "";


    public DateTime Fecha { get; set; }

    public DetallePedido[] Detalles { get; set; } = new DetallePedido[0];


    public int TiempoTraslado { get; set; }

    public Venta() { }


    public static Venta DesdePedido(Pedido pedido)
    {
        if (pedido == null)
            throw new ArgumentNullException(nameof(pedido));
        if (pedido.Estado != EstadoPedido.Entregado || pedido.FechaEntrega == null)
            throw new InvalidOperationException("Solo se registra la venta de un pedido entregado.");

        Venta venta = new Venta();
        venta.Id = "V-" + pedido.Id;
        venta.IdPedido = pedido.Id;
        venta.CodigoSede = pedido.CodigoSede;
        venta.Fecha = pedido.FechaEntrega.Value;
        venta.Detalles = pedido.Detalles;
        venta.TiempoTraslado = pedido.TiempoTraslado;
        return venta;
    }

    public decimal MontoTotal
    {
        get
        {
            decimal total = 0;
            for (int i = 0; i < Detalles.Length; i++)
                total += Detalles[i].Subtotal;
            return total;
        }
    }

  
    public decimal Rendimiento => Pedido.CalcularRendimiento(MontoTotal, TiempoTraslado);
}