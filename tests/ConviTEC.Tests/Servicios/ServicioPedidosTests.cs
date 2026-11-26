using ConviTEC.Core.Grafo;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

// Pruebas del registro de pedidos en la sede receptora (rol C; diseño 5.6, D-01, D-02, D-03).
// La sede receptora es LIM (en "Limón"). Las respuestas de las otras sedes se simulan a mano.
public class ServicioPedidosTests
{
    private readonly InventarioFalso local = new InventarioFalso(("P040", 5), ("P007", 0));
    private readonly InventarioGlobalFalso global = new InventarioGlobalFalso("LIB", "ALA", "SJO", "LIM")
        .Producto("P040", "Agua 600 ml", 500m)
        .Producto("P007", "Café 250 g", 2000m)
        .Fijar("SJO", "P040", 20).Fijar("SJO", "P007", 4)
        .Fijar("ALA", "P007", 3)
        .Fijar("LIB", "P040", 1);
    private readonly RegistroPedidosFalso registro = new RegistroPedidosFalso();
    private readonly BusFalso bus = new BusFalso();
    private readonly ServicioPedidos servicio;

    public ServicioPedidosTests()
    {
        servicio = Crear(TimeSpan.FromSeconds(10));
    }

    // Grafo: Limón — San José — Cartago, y aparte Liberia — Cañas (aislado, como Guanacaste).
    internal static ServicioRutas Rutas()
    {
        var grafo = new GrafoDistribucion();
        grafo.AgregarRuta("Limón", "San José", 120);
        grafo.AgregarRuta("San José", "Cartago", 30);
        grafo.AgregarRuta("Liberia", "Cañas", 40);
        return new ServicioRutas(grafo);
    }

    private ServicioPedidos Crear(TimeSpan tiempoLimite)
    {
        return new ServicioPedidos("LIM", "Limón", local, global, registro, Rutas(), bus, tiempoLimite,
                                   () => new DateTime(2026, 11, 24, 10, 0, 0), _ => { });
    }

    private static LineaPedido[] Lineas(params (string Codigo, int Cantidad)[] lineas)
    {
        var resultado = new LineaPedido[lineas.Length];
        for (int i = 0; i < lineas.Length; i++)
            resultado[i] = new LineaPedido(lineas[i].Codigo, lineas[i].Cantidad);
        return resultado;
    }

    private static Sobre Respuesta(string sede, string idPedido, bool aceptada, string motivo = "")
    {
        return Sobre.ParaSede(sede, "LIM",
            new RespuestaReserva(ServicioPedidos.IdReserva(idPedido, sede), idPedido, aceptada, motivo));
    }

    private T[] Enviados<T>() where T : ContenidoMensaje
    {
        return bus.Enviados.Where(s => s.Contenido is T).Select(s => (T)s.Contenido).ToArray();
    }

    private string[] DestinosDe<T>() where T : ContenidoMensaje
    {
        return bus.Enviados.Where(s => s.Contenido is T).Select(s => s.SedeDestino!).ToArray();
    }

    // ---------- Rechazos tempranos: no reservan nada ----------

    [Fact]
    public async Task SinLineas_SeRechaza()
    {
        ResultadoPedido r = await servicio.RegistrarAsync("San José", new LineaPedido[0]);

        Assert.False(r.Aceptado);
        Assert.Null(r.Pedido);
    }

    [Fact]
    public async Task CantidadCero_SeRechaza()
    {
        ResultadoPedido r = await servicio.RegistrarAsync("San José", Lineas(("P040", 0)));

        Assert.False(r.Aceptado);
        Assert.Contains("P040", r.Mensaje);
    }

    [Fact]
    public async Task ProductoQueNoEstaEnElCatalogo_SeRechaza()
    {
        ResultadoPedido r = await servicio.RegistrarAsync("San José", Lineas(("P999", 1)));

        Assert.False(r.Aceptado);
        Assert.Contains("P999", r.Mensaje);
    }

    [Fact]
    public async Task DestinoInalcanzable_SeRechazaSinReservarNiMandarNada()
    {
        // D-01: la ruta se revisa antes de reservar.
        ResultadoPedido r = await servicio.RegistrarAsync("Cañas", Lineas(("P040", 8)));

        Assert.False(r.Aceptado);
        Assert.Contains("No hay ruta", r.Mensaje);
        Assert.Equal(0, local.Reservado("P040"));
        Assert.Empty(bus.Enviados);
        Assert.Equal(1, registro.SiguienteNumero());   // no se gastó ningún número de pedido
    }

    [Fact]
    public async Task DestinoEsLaMismaSede_SeRechaza()
    {
        // D-06
        ResultadoPedido r = await servicio.RegistrarAsync("Limón", Lineas(("P040", 1)));

        Assert.False(r.Aceptado);
        Assert.Contains("misma sede", r.Mensaje);
    }

    [Fact]
    public async Task NiSumandoLaCadenaAlcanza_SeRechazaDiciendoCuantoHay()
    {
        // P007: aquí 0, SJO 4, ALA 3 → hay 7.
        ResultadoPedido r = await servicio.RegistrarAsync("San José", Lineas(("P007", 10)));

        Assert.False(r.Aceptado);
        Assert.Contains("Café 250 g", r.Mensaje);
        Assert.Contains("hay 7", r.Mensaje);
        Assert.Empty(bus.Enviados);
    }

    // ---------- Solo con inventario local ----------

    [Fact]
    public async Task SoloLocal_SeConfirmaDeUnaVez()
    {
        ResultadoPedido r = await servicio.RegistrarAsync("San José", Lineas(("P040", 3)));

        Assert.True(r.Aceptado);
        Assert.Equal("LIM-0001", r.Pedido!.Id);
        Assert.Equal(EstadoPedido.Pendiente, r.Pedido.Estado);
        Assert.Equal(2, local.Cantidad("P040"));
        Assert.Equal(0, local.Reservado("P040"));
        Assert.Empty(Enviados<SolicitudReserva>());
        Assert.Same(r.Pedido, registro.Buscar("LIM-0001"));
    }

    [Fact]
    public async Task SoloLocal_DifundeLoQueQuedaDisponible()
    {
        await servicio.RegistrarAsync("San José", Lineas(("P040", 3)));

        var act = Assert.Single(Enviados<ActualizacionInventario>());
        Assert.Equal("P040", act.Lineas[0].Codigo);
        Assert.Equal(2, act.Lineas[0].Cantidad);
    }

    [Fact]
    public async Task LineasRepetidas_SeSuman()
    {
        ResultadoPedido r = await servicio.RegistrarAsync("San José", Lineas(("P040", 2), ("P040", 2)));

        var detalle = Assert.Single(r.Pedido!.Detalles);
        Assert.Equal(4, detalle.Cantidad);
    }

    [Fact]
    public async Task SinRabbitMQ_SoloLocalSiSeAcepta()
    {
        bus.Fallar = true;   // Conectado = false

        ResultadoPedido r = await servicio.RegistrarAsync("San José", Lineas(("P040", 3)));

        Assert.True(r.Aceptado);
    }

    [Fact]
    public async Task SinRabbitMQ_SiNecesitaOtrasSedes_SeRechazaSinReservar()
    {
        bus.Fallar = true;

        ResultadoPedido r = await servicio.RegistrarAsync("San José", Lineas(("P040", 8)));

        Assert.False(r.Aceptado);
        Assert.Contains("No hay comunicación", r.Mensaje);
        Assert.Equal(0, local.Reservado("P040"));
    }

    [Fact]
    public async Task LosIdsSonConsecutivosConElCodigoDeLaSede()
    {
        ResultadoPedido a = await servicio.RegistrarAsync("San José", Lineas(("P040", 1)));
        ResultadoPedido b = await servicio.RegistrarAsync("Cartago", Lineas(("P040", 1)));

        Assert.Equal("LIM-0001", a.Pedido!.Id);
        Assert.Equal("LIM-0002", b.Pedido!.Id);
    }

    // ---------- Asignación de proveedores (D-02) ----------

    [Fact]
    public async Task Asignacion_PrimeroLocalYElFaltanteALaQueTieneMas()
    {
        // P040: aquí 5; SJO 20 y LIB 1 → los 3 que faltan salen de SJO.
        Task<ResultadoPedido> tarea = servicio.RegistrarAsync("San José", Lineas(("P040", 8)));

        var solicitud = Assert.Single(Enviados<SolicitudReserva>());
        Assert.Equal("SJO", Assert.Single(DestinosDe<SolicitudReserva>()));
        Assert.Equal("LIM-0001-SJO", solicitud.IdReserva);
        Assert.Equal("LIM-0001", solicitud.IdPedido);
        Assert.Equal(3, solicitud.Lineas[0].Cantidad);
        Assert.Equal(5, local.Reservado("P040"));   // lo local ya quedó apartado

        await servicio.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", true));
        await tarea;
    }

    [Fact]
    public async Task Asignacion_SiUnaSedeNoAlcanza_SeRepartenEntreVarias()
    {
        // P007: aquí 0; SJO 4, ALA 3 → 4 de SJO y 2 de ALA.
        Task<ResultadoPedido> tarea = servicio.RegistrarAsync("San José", Lineas(("P007", 6)));

        SolicitudReserva[] solicitudes = Enviados<SolicitudReserva>();
        string[] destinos = DestinosDe<SolicitudReserva>();
        Assert.Equal(2, solicitudes.Length);
        Assert.Equal(4, solicitudes[Array.IndexOf(destinos, "SJO")].Lineas[0].Cantidad);
        Assert.Equal(2, solicitudes[Array.IndexOf(destinos, "ALA")].Lineas[0].Cantidad);

        await servicio.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", true));
        await servicio.AlRecibirRespuestaAsync(Respuesta("ALA", "LIM-0001", true));
        ResultadoPedido r = await tarea;

        Assert.True(r.Aceptado);
        Assert.Equal(new[] { "SJO", "ALA" }, r.Pedido!.SedesParticipantes());
    }

    // ---------- Fase 2: confirmar o cancelar ----------

    [Fact]
    public async Task TodasAceptan_PedidoPendienteYSeConfirmaEnCadaSede()
    {
        Task<ResultadoPedido> tarea = servicio.RegistrarAsync("San José", Lineas(("P040", 8)));
        Assert.Equal(EstadoPedido.Registrado, registro.Buscar("LIM-0001")!.Estado);

        await servicio.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", true));
        ResultadoPedido r = await tarea;

        Assert.True(r.Aceptado);
        Assert.Equal(EstadoPedido.Pendiente, registro.Buscar("LIM-0001")!.Estado);
        Assert.Equal(0, local.Cantidad("P040"));
        Assert.Equal(0, local.Reservado("P040"));
        Assert.Equal("LIM-0001-SJO", Assert.Single(Enviados<ConfirmarReserva>()).IdReserva);
        Assert.Empty(Enviados<LiberarReserva>());
    }

    [Fact]
    public async Task UnaSedeRechaza_SeCancelaYSeLiberaLoLocal()
    {
        Task<ResultadoPedido> tarea = servicio.RegistrarAsync("San José", Lineas(("P040", 8)));

        await servicio.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", false, "No hay suficientes unidades de P040"));
        ResultadoPedido r = await tarea;

        Assert.False(r.Aceptado);
        Assert.Contains("SJO no pudo reservar", r.Mensaje);
        Assert.Contains("No hay suficientes unidades de P040", r.Pedido!.MotivoCancelacion);
        Assert.Equal(EstadoPedido.Cancelado, registro.Buscar("LIM-0001")!.Estado);
        Assert.Equal(5, local.Cantidad("P040"));
        Assert.Equal(0, local.Reservado("P040"));
        Assert.Empty(Enviados<ConfirmarReserva>());
        Assert.Empty(Enviados<LiberarReserva>());   // SJO rechazó: no apartó nada
    }

    [Fact]
    public async Task UnaAceptaYOtraRechaza_SoloSeLiberaEnLaQueAcepto()
    {
        Task<ResultadoPedido> tarea = servicio.RegistrarAsync("San José", Lineas(("P007", 6)));

        await servicio.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", true));
        await servicio.AlRecibirRespuestaAsync(Respuesta("ALA", "LIM-0001", false, "Sin existencias"));
        ResultadoPedido r = await tarea;

        Assert.False(r.Aceptado);
        Assert.Equal("SJO", Assert.Single(DestinosDe<LiberarReserva>()));
    }

    [Fact]
    public async Task NadieResponde_SeCancelaPorTiempoLimiteYSePideLiberar()
    {
        ServicioPedidos rapido = Crear(TimeSpan.FromMilliseconds(200));

        ResultadoPedido r = await rapido.RegistrarAsync("San José", Lineas(("P040", 8)));

        Assert.False(r.Aceptado);
        Assert.Contains("No respondió a tiempo: SJO", r.Mensaje);
        Assert.Equal(0, local.Reservado("P040"));
        // Quizá SJO sí apartó y su respuesta se perdió: se le pide liberar por si acaso.
        Assert.Equal("LIM-0001-SJO", Assert.Single(Enviados<LiberarReserva>()).IdReserva);
    }

    [Fact]
    public async Task RespuestaTardiaAceptada_DeUnPedidoCancelado_SeContestaConLiberar()
    {
        ServicioPedidos rapido = Crear(TimeSpan.FromMilliseconds(100));
        await rapido.RegistrarAsync("San José", Lineas(("P040", 8)));
        int liberacionesAntes = Enviados<LiberarReserva>().Length;

        await rapido.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", true));

        Assert.Equal(liberacionesAntes + 1, Enviados<LiberarReserva>().Length);
        Assert.Equal("SJO", bus.Enviados[^1].SedeDestino);
    }

    [Fact]
    public async Task RespuestaRepetida_DeUnPedidoConfirmado_SeIgnora()
    {
        Task<ResultadoPedido> tarea = servicio.RegistrarAsync("San José", Lineas(("P040", 8)));
        await servicio.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", true));
        await tarea;
        int enviadosAntes = bus.Enviados.Count;

        await servicio.AlRecibirRespuestaAsync(Respuesta("SJO", "LIM-0001", true));

        Assert.Equal(enviadosAntes, bus.Enviados.Count);
        Assert.Equal(EstadoPedido.Pendiente, registro.Buscar("LIM-0001")!.Estado);
    }

    [Fact]
    public async Task RespuestaDeUnaSedeQueNoParticipa_NoCuenta()
    {
        ServicioPedidos rapido = Crear(TimeSpan.FromMilliseconds(200));
        Task<ResultadoPedido> tarea = rapido.RegistrarAsync("San José", Lineas(("P040", 8)));

        await rapido.AlRecibirRespuestaAsync(Respuesta("ALA", "LIM-0001", true));

        Assert.False((await tarea).Aceptado);   // SJO nunca contestó
    }

    // ---------- Al arrancar ----------

    [Fact]
    public async Task CancelarPedidosEnEspera_LiberaLocalYPideLiberarAfuera()
    {
        // Así queda un pedido si la sede se apaga mientras espera: Registrado y con lo local apartado.
        var pedido = new Pedido("LIM-0007", "LIM", "San José", DateTime.Now, new[]
        {
            new DetallePedido("P040", "Agua 600 ml", 5, 500m, "LIM"),
            new DetallePedido("P040", "Agua 600 ml", 3, 500m, "SJO")
        });
        registro.Guardar(pedido);
        local.Reservar(new[] { new LineaReserva("P040", 5) }, out _);

        int cancelados = await servicio.CancelarPedidosEnEsperaAsync();

        Assert.Equal(1, cancelados);
        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
        Assert.Equal(0, local.Reservado("P040"));
        Assert.Equal("LIM-0007-SJO", Assert.Single(Enviados<LiberarReserva>()).IdReserva);
    }

    [Fact]
    public void RegistrarEn_AtiendeLasRespuestasDeReserva()
    {
        var despachador = new Despachador("LIM", registrar: _ => { });
        servicio.RegistrarEn(despachador);

        Assert.Throws<InvalidOperationException>(
            () => despachador.Registrar(TipoMensaje.RespuestaReserva, _ => Task.CompletedTask));
    }
}

// Dos sedes completas conectadas en memoria (sin RabbitMQ): el ejemplo de la Tabla 11.
// LIM recibe el pedido; SJO pone lo que falta con su ServicioReservas de verdad.
public class PedidoEntreSedesTests
{
    private readonly InventarioFalso inventarioLim = new InventarioFalso(("P040", 5));
    private readonly InventarioFalso inventarioSjo;
    private readonly RegistroPedidosFalso pedidosLim = new RegistroPedidosFalso();
    private readonly RedFalsa red = new RedFalsa();
    private readonly ServicioPedidos lim;

    public PedidoEntreSedesTests() : this(20) { }

    private PedidoEntreSedesTests(int stockRealSjo)
    {
        inventarioSjo = new InventarioFalso(("P040", stockRealSjo));
        lim = ArmarSedes(TimeSpan.FromSeconds(2));
    }

    private ServicioPedidos ArmarSedes(TimeSpan tiempoLimite)
    {
        // La copia global de LIM cree que SJO tiene 20 (puede estar atrasada).
        var globalLim = new InventarioGlobalFalso("LIM", "SJO").Producto("P040", "Agua 600 ml", 500m).Fijar("SJO", "P040", 20);

        var despachadorLim = new Despachador("LIM", registrar: _ => { });
        var despachadorSjo = new Despachador("SJO", registrar: _ => { });
        var busLim = red.Conectar("LIM", despachadorLim);
        var busSjo = red.Conectar("SJO", despachadorSjo);

        var pedidos = new ServicioPedidos("LIM", "Limón", inventarioLim, globalLim, pedidosLim,
                                          ServicioPedidosTests.Rutas(), busLim, tiempoLimite, registrar: _ => { });
        pedidos.RegistrarEn(despachadorLim);

        var reservasSjo = new ServicioReservas("SJO", inventarioSjo, new RegistroReservasFalso(), busSjo, registrar: _ => { });
        reservasSjo.RegistrarEn(despachadorSjo);
        return pedidos;
    }

    [Fact]
    public async Task PedidoConProductosDeOtraSede_SeConfirmaYDescuentaEnLasDos()
    {
        ResultadoPedido r = await lim.RegistrarAsync("San José", new[] { new LineaPedido("P040", 8) });
        await red.EsperarSilencioAsync();

        Assert.True(r.Aceptado, r.Mensaje);
        Assert.Equal(0, inventarioLim.Cantidad("P040"));
        Assert.Equal(17, inventarioSjo.Cantidad("P040"));
        Assert.Equal(0, inventarioSjo.Reservado("P040"));
    }

    [Fact]
    public async Task SiLaCopiaGlobalEstabaAtrasada_SjoRechazaYNadaQuedaApartado()
    {
        var prueba = new PedidoEntreSedesTests(stockRealSjo: 2);   // la copia dice 20, pero hay 2

        ResultadoPedido r = await prueba.lim.RegistrarAsync("San José", new[] { new LineaPedido("P040", 8) });
        await prueba.red.EsperarSilencioAsync();

        Assert.False(r.Aceptado);
        Assert.Contains("SJO no pudo reservar", r.Mensaje);
        Assert.Equal(0, prueba.inventarioLim.Reservado("P040"));
        Assert.Equal(0, prueba.inventarioSjo.Reservado("P040"));
    }

    [Fact]
    public async Task ConSjoApagada_SeCancelaPorTiempoLimite()
    {
        red.Apagar("SJO");
        ServicioPedidos rapido = ArmarSedes(TimeSpan.FromMilliseconds(300));

        ResultadoPedido r = await rapido.RegistrarAsync("San José", new[] { new LineaPedido("P040", 8) });

        Assert.False(r.Aceptado);
        Assert.Contains("No respondió a tiempo", r.Mensaje);
        Assert.Equal(5, inventarioLim.Cantidad("P040"));
        Assert.Equal(0, inventarioLim.Reservado("P040"));
    }
}
