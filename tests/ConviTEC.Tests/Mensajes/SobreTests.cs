using ConviTEC.Core.Mensajes;

namespace ConviTEC.Tests.Mensajes;

// Pruebas del sobre común y de los contenidos de mensaje (rol C).
public class SobreTests
{
    // ---------- Casos normales ----------

    [Fact]
    public void ParaSede_ArmaUnMensajeDirecto()
    {
        var contenido = new ConfirmarReserva("LIM-0007-SJO");

        Sobre sobre = Sobre.ParaSede("LIM", "SJO", contenido);

        Assert.Equal("LIM", sobre.SedeOrigen);
        Assert.Equal("SJO", sobre.SedeDestino);
        Assert.False(sobre.EsDifusion);
        Assert.Same(contenido, sobre.Contenido);
    }

    [Fact]
    public void ParaTodas_ArmaUnaDifusionSinDestino()
    {
        Sobre sobre = Sobre.ParaTodas("LIM", new SolicitudSincronizacion());

        Assert.Null(sobre.SedeDestino);
        Assert.True(sobre.EsDifusion);
    }

    [Fact]
    public void ElTipoDelSobreSaleDelContenido()
    {
        Sobre sobre = Sobre.ParaSede("LIM", "SJO", new LiberarReserva("LIM-0007-SJO", "Tiempo límite agotado"));

        Assert.Equal(TipoMensaje.LiberarReserva, sobre.Tipo);
    }

    [Fact]
    public void ElIdEmpiezaConLaSedeDeOrigen()
    {
        Sobre sobre = Sobre.ParaTodas("ALA", new SolicitudSincronizacion());

        Assert.StartsWith("ALA-", sobre.IdMensaje);
    }

    [Fact]
    public void DosMensajesNuncaTienenElMismoId()
    {
        Sobre a = Sobre.ParaTodas("LIB", new SolicitudSincronizacion());
        Sobre b = Sobre.ParaTodas("LIB", new SolicitudSincronizacion());

        Assert.NotEqual(a.IdMensaje, b.IdMensaje);
    }

    [Fact]
    public void ElConstructorCompletoConservaIdYFechaRecibidos()
    {
        // Así reconstruye la mensajería un mensaje que llega: con el id y la fecha del emisor.
        var fecha = new DateTimeOffset(2026, 11, 20, 10, 42, 13, TimeSpan.FromHours(-6));

        var sobre = new Sobre("LIB-abc123", "LIB", "SJO", fecha, new ConfirmarReserva("LIB-0042-SJO"));

        Assert.Equal("LIB-abc123", sobre.IdMensaje);
        Assert.Equal(fecha, sobre.FechaHora);
    }

    [Fact]
    public void SolicitudReserva_GuardaSusLineas()
    {
        var lineas = new LineaMensaje[] { new LineaMensaje("P040", 6), new LineaMensaje("P012", 2) };

        var solicitud = new SolicitudReserva("LIB-0042-SJO", "LIB-0042", lineas);

        Assert.Equal(2, solicitud.Lineas.Length);
        Assert.Equal("P040", solicitud.Lineas[0].Codigo);
        Assert.Equal(6, solicitud.Lineas[0].Cantidad);
    }

    [Fact]
    public void CadaContenidoTieneSuPropioTipo()
    {
        Assert.Equal(TipoMensaje.SolicitudReserva, new SolicitudReserva().Tipo);
        Assert.Equal(TipoMensaje.RespuestaReserva, new RespuestaReserva().Tipo);
        Assert.Equal(TipoMensaje.ConfirmarReserva, new ConfirmarReserva().Tipo);
        Assert.Equal(TipoMensaje.LiberarReserva, new LiberarReserva().Tipo);
        Assert.Equal(TipoMensaje.ActualizacionInventario, new ActualizacionInventario().Tipo);
        Assert.Equal(TipoMensaje.SolicitudSincronizacion, new SolicitudSincronizacion().Tipo);
        Assert.Equal(TipoMensaje.EstadoInventario, new EstadoInventario().Tipo);
    }

    [Fact]
    public void LasListasDeLineasEmpiezanVaciasYNoNulas()
    {
        Assert.Empty(new SolicitudReserva().Lineas);
        Assert.Empty(new ActualizacionInventario().Lineas);
        Assert.Empty(new EstadoInventario().Lineas);
    }

    // ---------- Casos excepcionales ----------

    [Fact]
    public void SinIdLanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() =>
            new Sobre("", "LIB", "SJO", DateTimeOffset.Now, new ConfirmarReserva("x")));
    }

    [Fact]
    public void SinSedeDeOrigenLanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => Sobre.ParaTodas(" ", new SolicitudSincronizacion()));
    }

    [Fact]
    public void DestinoVacioLanzaExcepcion()
    {
        // Para difundir se usa null (ParaTodas), nunca un texto vacío.
        Assert.Throws<ArgumentException>(() => Sobre.ParaSede("LIB", "", new ConfirmarReserva("x")));
    }

    [Fact]
    public void SinContenidoLanzaExcepcion()
    {
        Assert.Throws<ArgumentNullException>(() => Sobre.ParaSede("LIB", "SJO", null!));
    }
}
