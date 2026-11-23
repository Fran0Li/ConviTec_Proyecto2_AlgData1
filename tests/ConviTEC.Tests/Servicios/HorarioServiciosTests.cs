using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

public class HorarioSalidasTests
{
    private static DateTime Hora(int hora, int minuto)
    {
        return new DateTime(2026, 11, 26, hora, minuto, 0);
    }

    private static Pedido Pedido(string id, DateTime creado, bool confirmar)
    {
        var pedido = new Pedido(id, "ALA", "Heredia", creado,
            new[] { new DetallePedido("P001", "Agua", 1, 500m, "ALA") });
        if (confirmar)
            pedido.Confirmar();
        return pedido;
    }

    // Casos normales

    [Fact]
    public void AntesDeLas11_SaleALas11()
    {
        Assert.Equal(Hora(11, 0), HorarioSalidas.ProximaSalida(Hora(8, 30)));
    }

    [Fact]
    public void JustoALas11_SaleALas11()
    {
        Assert.Equal(Hora(11, 0), HorarioSalidas.ProximaSalida(Hora(11, 0)));
    }

    [Fact]
    public void DespuesDeLas11_SaleALas15()
    {
        Assert.Equal(Hora(15, 0), HorarioSalidas.ProximaSalida(Hora(12, 45)));
    }

    [Fact]
    public void DespuesDeLas15_SaleMananaALas11()
    {
        Assert.Equal(new DateTime(2026, 11, 27, 11, 0, 0), HorarioSalidas.ProximaSalida(Hora(16, 10)));
    }

    [Fact]
    public void SoloEntranPendientesCreadosAntesDeLaSalida()
    {
        var pedidos = new[]
        {
            Pedido("A1", Hora(9, 0), true),     // entra
            Pedido("A2", Hora(11, 0), true),    // justo a la hora: entra
            Pedido("A3", Hora(12, 0), true),    // es para la salida de las 15:00
            Pedido("A4", Hora(9, 0), false)     // todavía no está confirmado
        };

        Pedido[] salida = HorarioSalidas.PedidosParaSalida(pedidos, Hora(11, 0));

        Assert.Equal(2, salida.Length);
        Assert.Equal("A1", salida[0].Id);
        Assert.Equal("A2", salida[1].Id);
    }

    // Casos excepcionales

    [Fact]
    public void ListaVacia_DevuelveVacio()
    {
        Assert.Empty(HorarioSalidas.PedidosParaSalida(new Pedido[0], Hora(11, 0)));
    }

    [Fact]
    public void ListaNula_LanzaExcepcion()
    {
        Assert.Throws<ArgumentNullException>(() => HorarioSalidas.PedidosParaSalida(null!, Hora(11, 0)));
    }
}