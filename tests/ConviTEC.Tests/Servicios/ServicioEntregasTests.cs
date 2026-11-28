using ConviTEC.Core.Grafo;
using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

public class ServicioEntregasTests
{
    private static readonly DateTime Salida = new DateTime(2026, 11, 26, 11, 0, 0);
    private static readonly Sede Alajuela = new Sede("ALA", "ConviTEC Alajuela", "Alajuela");

    // Parte de la red del enunciado
    private static GrafoDistribucion Red()
    {
        return CargadorGrafo.DesdeRutas(new Ruta[]
        {
            new Ruta("R05", "Alajuela", "San José", 35),
            new Ruta("R06", "Alajuela", "Heredia", 20),
            new Ruta("R07", "San José", "Heredia", 20),
            new Ruta("R08", "San José", "Cartago", 45)
        });
    }

    // Pedido confirmado y guardado en el registro
    private static Pedido Pendiente(RegistroPedidosFalso registro, string id, string destino, DateTime creado)
    {
        var pedido = new Pedido(id, "ALA", destino, creado,
            new[] { new DetallePedido("P001", "Agua", 4, 500m, "ALA") });
        pedido.Confirmar();
        registro.Guardar(pedido);
        return pedido;
    }

    // Casos normales

    [Fact]
    public void EntregaYCreaUnaVentaPorPedido()
    {
        var registro = new RegistroPedidosFalso();
        var pendientes = new[]
        {
            Pendiente(registro, "ALA-0001", "Cartago", Salida.AddHours(-2)),
            Pendiente(registro, "ALA-0002", "Heredia", Salida.AddHours(-1))
        };
        var servicio = new ServicioEntregas(Red(), registro);

        Recorrido recorrido = servicio.PlanificarSalida(Alajuela, pendientes, Salida);
        Venta[] ventas = servicio.RegistrarEntregas(recorrido);

        // Heredia (20) y luego Cartago (20 + 20 + 45 = 85)
        Assert.Equal(2, ventas.Length);
        Assert.Equal("V-ALA-0002", ventas[0].Id);
        Assert.Equal(20, ventas[0].TiempoTraslado);
        Assert.Equal(85, ventas[1].TiempoTraslado);
        Assert.Equal(2000m, ventas[1].MontoTotal);
    }

    [Fact]
    public void ElPedidoQuedaEntregadoYGuardado()
    {
        var registro = new RegistroPedidosFalso();
        var pendientes = new[] { Pendiente(registro, "ALA-0001", "Heredia", Salida.AddHours(-1)) };
        var servicio = new ServicioEntregas(Red(), registro);

        servicio.RegistrarEntregas(servicio.PlanificarSalida(Alajuela, pendientes, Salida));

        Pedido guardado = registro.Buscar("ALA-0001")!;
        Assert.Equal(EstadoPedido.Entregado, guardado.Estado);
        Assert.Equal(Salida.AddMinutes(20), guardado.FechaEntrega);
        Assert.Equal(20, guardado.TiempoTraslado);
    }

    [Fact]
    public void PedidosDespuesDeLaSalida_EsperanLaSiguiente()
    {
        var registro = new RegistroPedidosFalso();
        var pendientes = new[]
        {
            Pendiente(registro, "ALA-0001", "Heredia", Salida.AddHours(-1)),
            Pendiente(registro, "ALA-0002", "Cartago", Salida.AddHours(1))
        };
        var servicio = new ServicioEntregas(Red(), registro);

        Venta[] ventas = servicio.RegistrarEntregas(servicio.PlanificarSalida(Alajuela, pendientes, Salida));

        Assert.Single(ventas);
        Assert.Equal(EstadoPedido.Pendiente, registro.Buscar("ALA-0002")!.Estado);
    }

    // Casos excepcionales

    [Fact]
    public void EntregarDosVeces_NoDuplicaVentas()
    {
        var registro = new RegistroPedidosFalso();
        var pendientes = new[] { Pendiente(registro, "ALA-0001", "Heredia", Salida.AddHours(-1)) };
        var servicio = new ServicioEntregas(Red(), registro);
        Recorrido recorrido = servicio.PlanificarSalida(Alajuela, pendientes, Salida);

        servicio.RegistrarEntregas(recorrido);
        Venta[] segundaVez = servicio.RegistrarEntregas(recorrido);

        Assert.Empty(segundaVez);
    }

    [Fact]
    public void PedidoQueNoEstaEnElRegistro_SeSalta()
    {
        var recorrido = new Recorrido();
        recorrido.FechaSalida = Salida;
        recorrido.Paradas = new[]
        {
            new ParadaRecorrido { Destino = "Heredia", TiempoAcumulado = 20, IdsPedidos = new[] { "ALA-9999" } }
        };

        Venta[] ventas = new ServicioEntregas(Red(), new RegistroPedidosFalso()).RegistrarEntregas(recorrido);

        Assert.Empty(ventas);
    }

    [Fact]
    public void SinPendientes_SinVentas()
    {
        var servicio = new ServicioEntregas(Red(), new RegistroPedidosFalso());

        Assert.Empty(servicio.RegistrarEntregas(servicio.PlanificarSalida(Alajuela, new Pedido[0], Salida)));
    }

    [Fact]
    public void ArgumentosNulos_LanzaExcepcion()
    {
        Assert.Throws<ArgumentNullException>(() => new ServicioEntregas(Red(), null!));
        Assert.Throws<ArgumentNullException>(() => new ServicioEntregas(null!, new RegistroPedidosFalso()));
        Assert.Throws<ArgumentNullException>(() =>
            new ServicioEntregas(Red(), new RegistroPedidosFalso()).RegistrarEntregas(null!));
    }

    // Paso a paso

    [Fact]
    public void PasoAPaso_EntregaUnoPorUnoHastaTerminar()
    {
        var registro = new RegistroPedidosFalso();
        Pendiente(registro, "ALA-0001", "Cartago", Salida.AddHours(-2));
        Pendiente(registro, "ALA-0002", "Heredia", Salida.AddHours(-1));
        var servicio = new ServicioEntregas(Red(), registro);

        // 1. Desde la sede: Heredia (20)
        PasoEntrega p1 = servicio.EntregarSiguiente(Alajuela, "Alajuela", 0, Pendientes(registro), Salida);
        Assert.Equal("Heredia", p1.Parada!.Destino);
        Assert.Equal(20, p1.Ventas[0].TiempoTraslado);

        // 2. Desde Heredia: Cartago (20 + 45 = 65), acumulado 85
        PasoEntrega p2 = servicio.EntregarSiguiente(Alajuela, p1.Parada.Destino, p1.Parada.TiempoAcumulado,
            Pendientes(registro), Salida);
        Assert.Equal("Cartago", p2.Parada!.Destino);
        Assert.Equal(65, p2.Parada.TiempoTramo);
        Assert.Equal(85, p2.Ventas[0].TiempoTraslado);

        // 3. No queda nada
        PasoEntrega p3 = servicio.EntregarSiguiente(Alajuela, "Cartago", 85, Pendientes(registro), Salida);
        Assert.True(p3.Terminado);
        Assert.Empty(p3.Ventas);
    }

    [Fact]
    public void PasoAPaso_MinutosNegativos_LanzaExcepcion()
    {
        var servicio = new ServicioEntregas(Red(), new RegistroPedidosFalso());

        Assert.Throws<ArgumentException>(() =>
            servicio.EntregarSiguiente(Alajuela, "Alajuela", -5, new Pedido[0], Salida));
    }

    // Lo que devolvería la sede como pendientes en cada momento
    private static Pedido[] Pendientes(RegistroPedidosFalso registro)
    {
        return new[] { registro.Buscar("ALA-0001")!, registro.Buscar("ALA-0002")! };
    }
}