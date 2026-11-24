using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

// Pruebas de las reservas en dos fases del lado de la sede dueña (rol C; diseño 5.6, 5.7, D-03).
// La sede dueña es SJO y la que pide es LIB. Todo con falsos: no necesitan RabbitMQ ni archivos.
public class ServicioReservasTests
{
    private readonly InventarioFalso inventario = new InventarioFalso(("P040", 10), ("P007", 3));
    private readonly RegistroReservasFalso registro = new RegistroReservasFalso();
    private readonly BusFalso bus = new BusFalso();
    private DateTime ahora = new DateTime(2026, 11, 23, 10, 0, 0);
    private readonly ServicioReservas servicio;

    public ServicioReservasTests()
    {
        servicio = new ServicioReservas("SJO", inventario, registro, bus, () => ahora, _ => { });
    }

    // ---------- Ayudantes ----------

    private static Sobre Solicitud(string idReserva, params (string Codigo, int Cantidad)[] lineas)
    {
        var lineasMensaje = new LineaMensaje[lineas.Length];
        for (int i = 0; i < lineas.Length; i++)
            lineasMensaje[i] = new LineaMensaje(lineas[i].Codigo, lineas[i].Cantidad);
        return Sobre.ParaSede("LIB", "SJO", new SolicitudReserva(idReserva, "LIB-0042", lineasMensaje));
    }

    private static Sobre Confirmar(string idReserva) => Sobre.ParaSede("LIB", "SJO", new ConfirmarReserva(idReserva));

    private static Sobre Liberar(string idReserva) => Sobre.ParaSede("LIB", "SJO", new LiberarReserva(idReserva, "Pedido cancelado"));

    private RespuestaReserva UltimaRespuesta()
    {
        Sobre ultimo = bus.Enviados[^1];
        Assert.Equal("LIB", ultimo.SedeDestino);
        return Assert.IsType<RespuestaReserva>(ultimo.Contenido);
    }

    // ---------- Fase 1: SolicitudReserva ----------

    [Fact]
    public async Task Solicitud_ConStock_ApartaYRespondeAceptada()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6), ("P007", 3)));

        RespuestaReserva r = UltimaRespuesta();
        Assert.True(r.Aceptada);
        Assert.Equal("LIB-0042-SJO", r.IdReserva);
        Assert.Equal("LIB-0042", r.IdPedido);
        Assert.Equal(6, inventario.Reservado("P040"));
        Assert.Equal(3, inventario.Reservado("P007"));
    }

    [Fact]
    public async Task Solicitud_ConStock_GuardaLaReservaPendienteConSuVencimiento()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));

        Reserva? reserva = registro.Buscar("LIB-0042-SJO");
        Assert.NotNull(reserva);
        Assert.Equal(EstadoReserva.Pendiente, reserva!.Estado);
        Assert.Equal("LIB", reserva.SedeSolicitante);
        Assert.Equal("SJO", reserva.SedeDuena);
        Assert.Equal(ahora.AddMinutes(5), reserva.FechaVencimiento);
    }

    [Fact]
    public async Task Solicitud_SinStock_NoApartaNadaYRespondeRechazadaConMotivo()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6), ("P007", 4)));

        RespuestaReserva r = UltimaRespuesta();
        Assert.False(r.Aceptada);
        Assert.Contains("P007", r.Motivo);
        Assert.Equal(0, inventario.Reservado("P040"));
        Assert.Equal(EstadoReserva.Rechazada, registro.Buscar("LIB-0042-SJO")!.Estado);
    }

    [Fact]
    public async Task Solicitud_Repetida_NoApartaDosVecesYReenviaLaRespuesta()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));

        Assert.Equal(6, inventario.Reservado("P040"));
        Assert.Equal(2, bus.Enviados.Count);
        Assert.True(UltimaRespuesta().Aceptada);
    }

    [Fact]
    public async Task Solicitud_RepetidaDeUnaRechazada_ReenviaElRechazo()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 50)));
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 50)));

        Assert.False(UltimaRespuesta().Aceptada);
        Assert.Equal(0, inventario.Reservado("P040"));
    }

    [Fact]
    public async Task Solicitud_RepetidaDeUnaYaLiberada_RespondeQueNoTieneUnidades()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        await servicio.AlRecibirLiberacionAsync(Liberar("LIB-0042-SJO"));

        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));

        RespuestaReserva r = UltimaRespuesta();
        Assert.False(r.Aceptada);
        Assert.Contains("liberada", r.Motivo);
        Assert.Equal(0, inventario.Reservado("P040"));
    }

    [Fact]
    public async Task Solicitud_ProductoInexistente_SeRechaza()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P999", 1)));

        RespuestaReserva r = UltimaRespuesta();
        Assert.False(r.Aceptada);
        Assert.Contains("P999", r.Motivo);
    }

    [Fact]
    public async Task Solicitud_SinLineas_SeRechazaSinGuardarNada()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO"));

        Assert.False(UltimaRespuesta().Aceptada);
        Assert.Null(registro.Buscar("LIB-0042-SJO"));
    }

    [Fact]
    public async Task Solicitud_ConCantidadCero_SeRechazaSinApartar()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6), ("P007", 0)));

        Assert.False(UltimaRespuesta().Aceptada);
        Assert.Equal(0, inventario.Reservado("P040"));
    }

    [Fact]
    public async Task Solicitud_SiNoSePuedeGuardar_DeshaceElApartadoYLanza()
    {
        registro.FallarAlGuardar = true;

        await Assert.ThrowsAsync<IOException>(() => servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6))));

        // La excepción devuelve el mensaje a la cola; al reintentar no debe haber nada apartado de antes.
        Assert.Equal(0, inventario.Reservado("P040"));
        Assert.Empty(bus.Enviados);
    }

    [Fact]
    public async Task Solicitud_SiFallaElEnvio_ElReintentoSoloReenviaLaRespuesta()
    {
        bus.Fallar = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6))));

        bus.Fallar = false;
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));

        Assert.Equal(6, inventario.Reservado("P040"));
        Assert.True(UltimaRespuesta().Aceptada);
    }

    // ---------- Fase 2a: ConfirmarReserva ----------

    [Fact]
    public async Task Confirmar_DescuentaYQuedaConfirmada()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));

        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));

        Assert.Equal(4, inventario.Cantidad("P040"));
        Assert.Equal(0, inventario.Reservado("P040"));
        Assert.Equal(EstadoReserva.Confirmada, registro.Buscar("LIB-0042-SJO")!.Estado);
    }

    [Fact]
    public async Task Confirmar_DifundeLosDisponiblesFinales()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6), ("P007", 1), ("P040", 2)));

        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));

        Sobre aviso = bus.Enviados[^1];
        Assert.True(aviso.EsDifusion);
        Assert.Equal("SJO", aviso.SedeOrigen);
        var act = Assert.IsType<ActualizacionInventario>(aviso.Contenido);
        Assert.Equal(2, act.Lineas.Length);   // P040 una sola vez aunque venía en dos líneas
        Assert.Equal("P040", act.Lineas[0].Codigo);
        Assert.Equal(2, act.Lineas[0].Cantidad);   // 10 - 8
        Assert.Equal("P007", act.Lineas[1].Codigo);
        Assert.Equal(2, act.Lineas[1].Cantidad);   // 3 - 1
    }

    [Fact]
    public async Task Confirmar_Repetido_NoDescuentaDosVeces()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));

        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));
        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));

        Assert.Equal(4, inventario.Cantidad("P040"));
    }

    [Fact]
    public async Task Confirmar_ReservaDesconocida_NoHaceNada()
    {
        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-9999-SJO"));

        Assert.Equal(10, inventario.Cantidad("P040"));
        Assert.Empty(bus.Enviados);
    }

    [Fact]
    public async Task Confirmar_ReservaYaLiberada_NoDescuenta()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        await servicio.AlRecibirLiberacionAsync(Liberar("LIB-0042-SJO"));

        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));

        Assert.Equal(10, inventario.Cantidad("P040"));
        Assert.Equal(EstadoReserva.Liberada, registro.Buscar("LIB-0042-SJO")!.Estado);
    }

    [Fact]
    public async Task Confirmar_SiFallaLaDifusion_LaConfirmacionQuedaHecha()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        bus.Fallar = true;

        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));

        Assert.Equal(4, inventario.Cantidad("P040"));
        Assert.Equal(EstadoReserva.Confirmada, registro.Buscar("LIB-0042-SJO")!.Estado);
    }

    // ---------- Fase 2b: LiberarReserva ----------

    [Fact]
    public async Task Liberar_DevuelveLasUnidades()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));

        await servicio.AlRecibirLiberacionAsync(Liberar("LIB-0042-SJO"));

        Assert.Equal(0, inventario.Reservado("P040"));
        Assert.Equal(10, inventario.Cantidad("P040"));
        Assert.Equal(EstadoReserva.Liberada, registro.Buscar("LIB-0042-SJO")!.Estado);
    }

    [Fact]
    public async Task Liberar_Repetido_NoDevuelveDosVeces()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0043-SJO", ("P040", 2)));

        await servicio.AlRecibirLiberacionAsync(Liberar("LIB-0042-SJO"));
        await servicio.AlRecibirLiberacionAsync(Liberar("LIB-0042-SJO"));

        // La otra reserva (2 unidades) sigue apartada: el segundo Liberar no le robó unidades.
        Assert.Equal(2, inventario.Reservado("P040"));
    }

    [Fact]
    public async Task Liberar_ReservaConfirmada_NoDevuelveNada()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));

        await servicio.AlRecibirLiberacionAsync(Liberar("LIB-0042-SJO"));

        Assert.Equal(4, inventario.Cantidad("P040"));
        Assert.Equal(EstadoReserva.Confirmada, registro.Buscar("LIB-0042-SJO")!.Estado);
    }

    [Fact]
    public async Task Liberar_ReservaDesconocida_NoHaceNada()
    {
        await servicio.AlRecibirLiberacionAsync(Liberar("LIB-9999-SJO"));

        Assert.Equal(0, inventario.Reservado("P040"));
    }

    // ---------- Vencimiento ----------

    [Fact]
    public async Task LiberarVencidas_AntesDeLos5Minutos_NoLiberaNada()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        ahora = ahora.AddMinutes(4).AddSeconds(59);

        Assert.Equal(0, servicio.LiberarVencidas());
        Assert.Equal(6, inventario.Reservado("P040"));
    }

    [Fact]
    public async Task LiberarVencidas_ALos5Minutos_LiberaLaPendiente()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        ahora = ahora.AddMinutes(5);

        Assert.Equal(1, servicio.LiberarVencidas());
        Assert.Equal(0, inventario.Reservado("P040"));
        Assert.Equal(EstadoReserva.Liberada, registro.Buscar("LIB-0042-SJO")!.Estado);
    }

    [Fact]
    public async Task LiberarVencidas_NoTocaLasConfirmadasNiLasRecientes()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0001-SJO", ("P040", 2)));
        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0001-SJO"));
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0002-SJO", ("P040", 3)));   // vencerá
        ahora = ahora.AddMinutes(3);
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0003-SJO", ("P040", 1)));   // reciente
        ahora = ahora.AddMinutes(2);

        Assert.Equal(1, servicio.LiberarVencidas());
        Assert.Equal(EstadoReserva.Confirmada, registro.Buscar("LIB-0001-SJO")!.Estado);
        Assert.Equal(EstadoReserva.Liberada, registro.Buscar("LIB-0002-SJO")!.Estado);
        Assert.Equal(EstadoReserva.Pendiente, registro.Buscar("LIB-0003-SJO")!.Estado);
        Assert.Equal(1, inventario.Reservado("P040"));
    }

    [Fact]
    public async Task ConfirmarDespuesDeVencida_NoDescuenta()
    {
        await servicio.AlRecibirSolicitudAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        ahora = ahora.AddMinutes(6);
        servicio.LiberarVencidas();

        await servicio.AlRecibirConfirmacionAsync(Confirmar("LIB-0042-SJO"));

        Assert.Equal(10, inventario.Cantidad("P040"));
    }

    // ---------- Con el despachador ----------

    [Fact]
    public async Task RegistrarEn_ElDespachadorLePasaLosTresTipos()
    {
        var despachador = new Despachador("SJO", registrar: _ => { });
        servicio.RegistrarEn(despachador);

        await despachador.ProcesarAsync(Solicitud("LIB-0042-SJO", ("P040", 6)));
        await despachador.ProcesarAsync(Confirmar("LIB-0042-SJO"));

        Assert.Equal(4, inventario.Cantidad("P040"));
        Assert.Throws<InvalidOperationException>(() => servicio.RegistrarEn(despachador));
    }

    [Fact]
    public void Constructor_SinCodigoDeSede_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => new ServicioReservas("", inventario, registro, bus));
    }
}
