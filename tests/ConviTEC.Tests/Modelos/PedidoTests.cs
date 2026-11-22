using ConviTEC.Core.Modelos;

namespace ConviTEC.Tests.Modelos;

// Pruebas de los modelos del dominio (rol B).
public class PedidoTests
{
    private static Pedido PedidoDeEjemplo()
    {
        var detalles = new DetallePedido[]
        {
            new DetallePedido("P001", "Agua 600 ml", 3, 500m, "LIB"),
            new DetallePedido("P040", "Café molido", 2, 2500m, "SJO"),
            new DetallePedido("P012", "Galletas", 1, 1000m, "LIB")
        };
        return new Pedido("LIB-0001", "LIB", "Cañas", new DateTime(2026, 11, 20, 9, 0, 0), detalles);
    }

    // Casos normales

    [Fact]
    public void UnPedidoNuevoEmpiezaRegistrado()
    {
        Assert.Equal(EstadoPedido.Registrado, PedidoDeEjemplo().Estado);
    }

    [Fact]
    public void MontoTotal_SumaLosSubtotales()
    {
        // 3*500 + 2*2500 + 1*1000 = 7500
        Assert.Equal(7500m, PedidoDeEjemplo().MontoTotal);
    }

    [Fact]
    public void SedesParticipantes_NoRepiteSedes()
    {
        string[] sedes = PedidoDeEjemplo().SedesParticipantes();

        Assert.Equal(new[] { "LIB", "SJO" }, sedes);
    }

    [Fact]
    public void DetallesDeSede_DevuelveSoloLasLineasDeEsaSede()
    {
        DetallePedido[] deLib = PedidoDeEjemplo().DetallesDeSede("LIB");

        Assert.Equal(2, deLib.Length);
        Assert.Equal("P001", deLib[0].CodigoProducto);
        Assert.Equal("P012", deLib[1].CodigoProducto);
    }

    [Fact]
    public void FlujoNormal_RegistradoPendienteEntregado()
    {
        Pedido pedido = PedidoDeEjemplo();
        var fecha = new DateTime(2026, 11, 20, 12, 5, 0);

        pedido.Confirmar();
        pedido.MarcarEntregado(fecha, 65);

        Assert.Equal(EstadoPedido.Entregado, pedido.Estado);
        Assert.Equal(fecha, pedido.FechaEntrega);
        Assert.Equal(65, pedido.TiempoTraslado);
    }

    [Fact]
    public void Rendimiento_EsMontoEntreMinutos()
    {
        Pedido pedido = PedidoDeEjemplo();
        pedido.Confirmar();
        pedido.MarcarEntregado(DateTime.Now, 75);

        Assert.Equal(100m, pedido.Rendimiento); // 7500 / 75
    }

    [Fact]
    public void Venta_SeCreaDesdeUnPedidoEntregado()
    {
        Pedido pedido = PedidoDeEjemplo();
        pedido.Confirmar();
        pedido.MarcarEntregado(new DateTime(2026, 11, 20, 12, 5, 0), 65);

        Venta venta = Venta.DesdePedido(pedido);

        Assert.Equal("V-LIB-0001", venta.Id);
        Assert.Equal("LIB", venta.CodigoSede);
        Assert.Equal(7500m, venta.MontoTotal);
        Assert.Equal(pedido.FechaEntrega, venta.Fecha);
    }

    // Casos excepcionales

    [Fact]
    public void PedidoSinProductos_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() =>
            new Pedido("LIB-0002", "LIB", "Cañas", DateTime.Now, new DetallePedido[0]));
    }

    [Fact]
    public void CantidadCeroOMenor_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => new DetallePedido("P001", "Agua", 0, 500m, "LIB"));
        Assert.Throws<ArgumentException>(() => new DetallePedido("P001", "Agua", -2, 500m, "LIB"));
    }

    [Fact]
    public void NoSeEntregaUnPedidoSinConfirmar()
    {
        Assert.Throws<InvalidOperationException>(() => PedidoDeEjemplo().MarcarEntregado(DateTime.Now, 10));
    }

    [Fact]
    public void NoSeCancelaUnPedidoEntregado()
    {
        Pedido pedido = PedidoDeEjemplo();
        pedido.Confirmar();
        pedido.MarcarEntregado(DateTime.Now, 10);

        Assert.Throws<InvalidOperationException>(() => pedido.Cancelar("x"));
    }

    [Fact]
    public void CancelarDosVeces_NoCambiaElPrimerMotivo()
    {
        Pedido pedido = PedidoDeEjemplo();

        pedido.Cancelar("Sin ruta");
        pedido.Cancelar("Otro motivo");

        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
        Assert.Equal("Sin ruta", pedido.MotivoCancelacion);
    }

    [Fact]
    public void NoHayVentaDeUnPedidoSinEntregar()
    {
        Assert.Throws<InvalidOperationException>(() => Venta.DesdePedido(PedidoDeEjemplo()));
    }

    [Fact]
    public void Rendimiento_ConTiempoCeroNoDivideEntreCero()
    {
        Pedido pedido = PedidoDeEjemplo();
        pedido.Confirmar();
        pedido.MarcarEntregado(DateTime.Now, 0);

        Assert.Equal(7500m, pedido.Rendimiento); // se toma como 1 minuto
    }

    [Fact]
    public void Reserva_LiberarDosVecesNoFalla()
    {
        var reserva = new Reserva("LIB-0001-SJO", "LIB-0001", "LIB", "SJO",
            new[] { new LineaReserva("P040", 2) }, DateTime.Now, TimeSpan.FromSeconds(30));

        reserva.Liberar();
        reserva.Liberar();

        Assert.Equal(EstadoReserva.Liberada, reserva.Estado);
    }

    [Fact]
    public void Reserva_VenceDespuesDeSuDuracion()
    {
        var inicio = new DateTime(2026, 11, 20, 10, 0, 0);
        var reserva = new Reserva("LIB-0001-SJO", "LIB-0001", "LIB", "SJO",
            new[] { new LineaReserva("P040", 2) }, inicio, TimeSpan.FromSeconds(30));

        Assert.False(reserva.EstaVencida(inicio.AddSeconds(10)));
        Assert.True(reserva.EstaVencida(inicio.AddSeconds(30)));
    }
}