using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

// Pruebas de la sincronización del inventario global (rol C; diseño 5.7, 5.10, RF-05).
// La sede es LIM; las demás son LIB, ALA y SJO. No necesitan RabbitMQ.
public class ServicioSincronizacionTests
{
    private readonly InventarioFalso local = new InventarioFalso(("P040", 10), ("P007", 3));
    private readonly InventarioGlobalFalso global = new InventarioGlobalFalso("LIB", "ALA", "SJO", "LIM")
        .Fijar("SJO", "P040", 20).Fijar("SJO", "P007", 4);
    private readonly BusFalso bus = new BusFalso();
    private readonly ServicioSincronizacion servicio;

    public ServicioSincronizacionTests()
    {
        servicio = new ServicioSincronizacion("LIM", local, global, bus, _ => { });
    }

    private static LineaMensaje[] Lineas(params (string Codigo, int Cantidad)[] lineas)
    {
        var resultado = new LineaMensaje[lineas.Length];
        for (int i = 0; i < lineas.Length; i++)
            resultado[i] = new LineaMensaje(lineas[i].Codigo, lineas[i].Cantidad);
        return resultado;
    }

    // ---------- ActualizacionInventario ----------

    [Fact]
    public async Task Actualizacion_CambiaSoloEsosProductosDeEsaSede()
    {
        await servicio.AlRecibirActualizacionAsync(Sobre.ParaTodas("SJO", new ActualizacionInventario(Lineas(("P040", 14)))));

        Assert.Equal(14, global.Disponible("SJO", "P040"));
        Assert.Equal(4, global.Disponible("SJO", "P007"));   // no venía: queda igual
    }

    [Fact]
    public async Task Actualizacion_Repetida_DejaElMismoResultado()
    {
        Sobre aviso = Sobre.ParaTodas("SJO", new ActualizacionInventario(Lineas(("P040", 14))));

        await servicio.AlRecibirActualizacionAsync(aviso);
        await servicio.AlRecibirActualizacionAsync(aviso);

        Assert.Equal(14, global.Disponible("SJO", "P040"));   // cantidad final, no "restar 6 dos veces"
    }

    [Fact]
    public async Task Actualizacion_DeSedeDesconocida_SeIgnora()
    {
        await servicio.AlRecibirActualizacionAsync(Sobre.ParaTodas("PRA", new ActualizacionInventario(Lineas(("P040", 99)))));

        Assert.Equal(0, global.Disponible("PRA", "P040"));
        Assert.Equal(0, global.VecesEscrita);
    }

    [Fact]
    public async Task Actualizacion_DeEstaMismaSede_SeIgnora()
    {
        await servicio.AlRecibirActualizacionAsync(Sobre.ParaTodas("LIM", new ActualizacionInventario(Lineas(("P040", 1)))));

        Assert.Equal(0, global.VecesEscrita);
    }

    [Fact]
    public async Task Actualizacion_ConLineasInvalidas_SoloAplicaLasBuenas()
    {
        var lineas = new[] { new LineaMensaje("P040", 14), new LineaMensaje("P007", -3), new LineaMensaje("", 5) };

        await servicio.AlRecibirActualizacionAsync(Sobre.ParaTodas("SJO", new ActualizacionInventario(lineas)));

        Assert.Equal(14, global.Disponible("SJO", "P040"));
        Assert.Equal(4, global.Disponible("SJO", "P007"));
    }

    // ---------- SolicitudSincronizacion ----------

    [Fact]
    public async Task Solicitud_SeContestaConElInventarioDisponibleDirectoAQuienPidio()
    {
        local.Reservar(new[] { new ConviTEC.Core.Modelos.LineaReserva("P040", 4) }, out _);

        await servicio.AlRecibirSolicitudAsync(Sobre.ParaTodas("ALA", new SolicitudSincronizacion()));

        Sobre respuesta = Assert.Single(bus.Enviados);
        Assert.Equal("LIM", respuesta.SedeOrigen);
        Assert.Equal("ALA", respuesta.SedeDestino);
        var estado = Assert.IsType<EstadoInventario>(respuesta.Contenido);
        Assert.Equal(2, estado.Lineas.Length);
        Assert.Equal("P007", estado.Lineas[0].Codigo);
        Assert.Equal(3, estado.Lineas[0].Cantidad);
        Assert.Equal("P040", estado.Lineas[1].Codigo);
        Assert.Equal(6, estado.Lineas[1].Cantidad);   // 10 - 4 reservadas
    }

    [Fact]
    public async Task Solicitud_DeSedeDesconocida_NoSeContesta()
    {
        await servicio.AlRecibirSolicitudAsync(Sobre.ParaTodas("PRA", new SolicitudSincronizacion()));

        Assert.Empty(bus.Enviados);
    }

    [Fact]
    public async Task Solicitud_SiFallaElEnvio_LanzaParaQueVuelvaALaCola()
    {
        bus.Fallar = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.AlRecibirSolicitudAsync(Sobre.ParaTodas("ALA", new SolicitudSincronizacion())));
    }

    // ---------- EstadoInventario ----------

    [Fact]
    public async Task Estado_ReemplazaTodoLoQueSeSabiaDeEsaSede()
    {
        await servicio.AlRecibirEstadoAsync(Sobre.ParaSede("SJO", "LIM", new EstadoInventario(Lineas(("P040", 7)))));

        Assert.Equal(7, global.Disponible("SJO", "P040"));
        Assert.Equal(0, global.Disponible("SJO", "P007"));   // no venía: queda en 0
    }

    [Fact]
    public async Task Estado_DeSedeDesconocida_SeIgnora()
    {
        await servicio.AlRecibirEstadoAsync(Sobre.ParaSede("PRB", "LIM", new EstadoInventario(Lineas(("P040", 7)))));

        Assert.Equal(20, global.Disponible("SJO", "P040"));
        Assert.Equal(0, global.VecesEscrita);
    }

    // ---------- Lo que manda esta sede ----------

    [Fact]
    public async Task PedirSincronizacion_DifundeLaSolicitud()
    {
        Assert.True(await servicio.PedirSincronizacionAsync());

        Sobre pedido = Assert.Single(bus.Enviados);
        Assert.True(pedido.EsDifusion);
        Assert.IsType<SolicitudSincronizacion>(pedido.Contenido);
    }

    [Fact]
    public async Task PedirSincronizacion_SinRabbitMQ_DevuelveFalseSinLanzar()
    {
        bus.Fallar = true;

        Assert.False(await servicio.PedirSincronizacionAsync());
    }

    [Fact]
    public async Task DifundirCambios_MandaLoDisponibleDeEsosProductos()
    {
        local.Reservar(new[] { new ConviTEC.Core.Modelos.LineaReserva("P040", 2) }, out _);

        Assert.True(await servicio.DifundirCambiosAsync(new[] { "P040" }));

        var act = Assert.IsType<ActualizacionInventario>(Assert.Single(bus.Enviados).Contenido);
        Assert.Equal(8, act.Lineas[0].Cantidad);
    }

    [Fact]
    public void RegistrarEn_AtiendeLosTresTipos()
    {
        var despachador = new Despachador("LIM", registrar: _ => { });
        servicio.RegistrarEn(despachador);

        Assert.Throws<InvalidOperationException>(() => despachador.Registrar(TipoMensaje.ActualizacionInventario, _ => Task.CompletedTask));
        Assert.Throws<InvalidOperationException>(() => despachador.Registrar(TipoMensaje.SolicitudSincronizacion, _ => Task.CompletedTask));
        Assert.Throws<InvalidOperationException>(() => despachador.Registrar(TipoMensaje.EstadoInventario, _ => Task.CompletedTask));
    }
}

// Tres sedes completas en memoria (sin RabbitMQ): la sincronización de punta a punta.
public class SincronizacionEntreSedesTests
{
    private readonly RedFalsa red = new RedFalsa();

    // Arma una sede con su inventario, su copia global (que empieza vacía) y sus servicios.
    private (InventarioFalso Local, InventarioGlobalFalso Global, ServicioSincronizacion Sync, ServicioReservas Reservas)
        Sede(string codigo, params (string, int)[] productos)
    {
        var local = new InventarioFalso(productos);
        var global = new InventarioGlobalFalso("LIB", "SJO", "LIM");
        var despachador = new Despachador(codigo, registrar: _ => { });
        var bus = red.Conectar(codigo, despachador);
        var sync = new ServicioSincronizacion(codigo, local, global, bus, _ => { });
        sync.RegistrarEn(despachador);
        var reservas = new ServicioReservas(codigo, local, new RegistroReservasFalso(), bus, registrar: _ => { });
        reservas.RegistrarEn(despachador);
        return (local, global, sync, reservas);
    }

    [Fact]
    public async Task AlArrancar_LaSedeRecibeElInventarioDeLasDemas()
    {
        var lib = Sede("LIB", ("P040", 3));
        var sjo = Sede("SJO", ("P040", 20), ("P007", 4));
        var lim = Sede("LIM", ("P040", 5));

        await lim.Sync.PedirSincronizacionAsync();
        await red.EsperarSilencioAsync();

        Assert.Equal(3, lim.Global.Disponible("LIB", "P040"));
        Assert.Equal(20, lim.Global.Disponible("SJO", "P040"));
        Assert.Equal(4, lim.Global.Disponible("SJO", "P007"));
        Assert.Equal(0, lim.Global.Disponible("LIM", "P040"));   // no guarda copia de sí misma
    }

    [Fact]
    public async Task AlConfirmarUnaReserva_LasDemasSedesSeEnteran()
    {
        var lib = Sede("LIB", ("P040", 3));
        var sjo = Sede("SJO", ("P040", 20));
        var lim = Sede("LIM", ("P040", 5));

        // LIM le reserva 6 a SJO y confirma (lo mismo que haría ServicioPedidos).
        var lineas = new[] { new LineaMensaje("P040", 6) };
        await sjo.Reservas.AlRecibirSolicitudAsync(Sobre.ParaSede("LIM", "SJO", new SolicitudReserva("LIM-0001-SJO", "LIM-0001", lineas)));
        await sjo.Reservas.AlRecibirConfirmacionAsync(Sobre.ParaSede("LIM", "SJO", new ConfirmarReserva("LIM-0001-SJO")));
        await red.EsperarSilencioAsync();

        // SJO difundió ActualizacionInventario: LIB y LIM ya saben que le quedan 14.
        Assert.Equal(14, lib.Global.Disponible("SJO", "P040"));
        Assert.Equal(14, lim.Global.Disponible("SJO", "P040"));
    }
}
