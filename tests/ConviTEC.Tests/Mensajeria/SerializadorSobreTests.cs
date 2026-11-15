using System.Text.Json;
using ConviTEC.Core.Mensajes;
using ConviTEC.Mensajeria;

namespace ConviTEC.Tests.Mensajeria;

// Pruebas del paso Sobre ↔ JSON (rol C, diseño 5.7).
public class SerializadorSobreTests
{
    // Serializa y vuelve a leer: es lo que pasa cuando un mensaje viaja de una sede a otra.
    private static Sobre IdaYVuelta(Sobre sobre)
    {
        return SerializadorSobre.Deserializar(SerializadorSobre.Serializar(sobre));
    }

    // ---------- Formato del JSON ----------

    [Fact]
    public void Serializar_UsaLosNombresDelDiseno()
    {
        var lineas = new[] { new LineaMensaje("P040", 6) };
        Sobre sobre = Sobre.ParaSede("LIB", "SJO", new SolicitudReserva("LIB-0042-SJO", "LIB-0042", lineas));

        using JsonDocument doc = JsonDocument.Parse(SerializadorSobre.Serializar(sobre));
        JsonElement raiz = doc.RootElement;

        Assert.Equal(sobre.IdMensaje, raiz.GetProperty("idMensaje").GetString());
        Assert.Equal("SolicitudReserva", raiz.GetProperty("tipo").GetString());
        Assert.Equal("LIB", raiz.GetProperty("sedeOrigen").GetString());
        Assert.Equal("SJO", raiz.GetProperty("sedeDestino").GetString());
        Assert.True(raiz.TryGetProperty("fechaHora", out _));

        JsonElement contenido = raiz.GetProperty("contenido");
        Assert.Equal("LIB-0042-SJO", contenido.GetProperty("idReserva").GetString());
        Assert.Equal("LIB-0042", contenido.GetProperty("idPedido").GetString());
        Assert.Equal("P040", contenido.GetProperty("lineas")[0].GetProperty("codigo").GetString());
        Assert.Equal(6, contenido.GetProperty("lineas")[0].GetProperty("cantidad").GetInt32());
    }

    [Fact]
    public void Serializar_NoRepiteElTipoDentroDelContenido()
    {
        Sobre sobre = Sobre.ParaSede("LIB", "SJO", new ConfirmarReserva("LIB-0042-SJO"));

        using JsonDocument doc = JsonDocument.Parse(SerializadorSobre.Serializar(sobre));

        Assert.False(doc.RootElement.GetProperty("contenido").TryGetProperty("tipo", out _));
    }

    [Fact]
    public void Serializar_DifusionLlevaDestinoNull()
    {
        Sobre sobre = Sobre.ParaTodas("ALA", new SolicitudSincronizacion());

        using JsonDocument doc = JsonDocument.Parse(SerializadorSobre.Serializar(sobre));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("sedeDestino").ValueKind);
    }

    // ---------- Ida y vuelta de cada tipo ----------

    [Fact]
    public void IdaYVuelta_ConservaLosDatosDelSobre()
    {
        Sobre original = Sobre.ParaSede("LIB", "SJO", new ConfirmarReserva("LIB-0042-SJO"));

        Sobre leido = IdaYVuelta(original);

        Assert.Equal(original.IdMensaje, leido.IdMensaje);
        Assert.Equal(original.SedeOrigen, leido.SedeOrigen);
        Assert.Equal(original.SedeDestino, leido.SedeDestino);
        Assert.Equal(original.FechaHora, leido.FechaHora);
        Assert.Equal(original.Tipo, leido.Tipo);
    }

    [Fact]
    public void IdaYVuelta_SolicitudReserva()
    {
        var lineas = new[] { new LineaMensaje("P040", 6), new LineaMensaje("P007", 2) };
        Sobre leido = IdaYVuelta(Sobre.ParaSede("LIB", "SJO", new SolicitudReserva("LIB-0042-SJO", "LIB-0042", lineas)));

        var c = Assert.IsType<SolicitudReserva>(leido.Contenido);
        Assert.Equal("LIB-0042-SJO", c.IdReserva);
        Assert.Equal("LIB-0042", c.IdPedido);
        Assert.Equal(2, c.Lineas.Length);
        Assert.Equal("P007", c.Lineas[1].Codigo);
        Assert.Equal(2, c.Lineas[1].Cantidad);
    }

    [Fact]
    public void IdaYVuelta_RespuestaReserva()
    {
        Sobre leido = IdaYVuelta(Sobre.ParaSede("SJO", "LIB",
            new RespuestaReserva("LIB-0042-SJO", "LIB-0042", false, "No hay suficientes unidades de P040")));

        var c = Assert.IsType<RespuestaReserva>(leido.Contenido);
        Assert.Equal("LIB-0042-SJO", c.IdReserva);
        Assert.Equal("LIB-0042", c.IdPedido);
        Assert.False(c.Aceptada);
        Assert.Equal("No hay suficientes unidades de P040", c.Motivo);
    }

    [Fact]
    public void IdaYVuelta_ConfirmarReserva()
    {
        Sobre leido = IdaYVuelta(Sobre.ParaSede("LIB", "SJO", new ConfirmarReserva("LIB-0042-SJO")));

        var c = Assert.IsType<ConfirmarReserva>(leido.Contenido);
        Assert.Equal("LIB-0042-SJO", c.IdReserva);
    }

    [Fact]
    public void IdaYVuelta_LiberarReserva()
    {
        Sobre leido = IdaYVuelta(Sobre.ParaSede("LIB", "SJO", new LiberarReserva("LIB-0042-SJO", "Tiempo límite agotado")));

        var c = Assert.IsType<LiberarReserva>(leido.Contenido);
        Assert.Equal("LIB-0042-SJO", c.IdReserva);
        Assert.Equal("Tiempo límite agotado", c.Motivo);
    }

    [Fact]
    public void IdaYVuelta_ActualizacionInventario()
    {
        Sobre leido = IdaYVuelta(Sobre.ParaTodas("SJO", new ActualizacionInventario(new[] { new LineaMensaje("P040", 14) })));

        Assert.True(leido.EsDifusion);
        var c = Assert.IsType<ActualizacionInventario>(leido.Contenido);
        Assert.Single(c.Lineas);
        Assert.Equal(14, c.Lineas[0].Cantidad);
    }

    [Fact]
    public void IdaYVuelta_SolicitudSincronizacion()
    {
        Sobre leido = IdaYVuelta(Sobre.ParaTodas("ALA", new SolicitudSincronizacion()));

        Assert.True(leido.EsDifusion);
        Assert.IsType<SolicitudSincronizacion>(leido.Contenido);
    }

    [Fact]
    public void IdaYVuelta_EstadoInventario()
    {
        var lineas = new[] { new LineaMensaje("P001", 10), new LineaMensaje("P002", 0), new LineaMensaje("P003", 5) };
        Sobre leido = IdaYVuelta(Sobre.ParaSede("SJO", "ALA", new EstadoInventario(lineas)));

        var c = Assert.IsType<EstadoInventario>(leido.Contenido);
        Assert.Equal(3, c.Lineas.Length);
        Assert.Equal(0, c.Lineas[1].Cantidad);
    }

    // ---------- Leer el ejemplo del documento de diseño ----------

    [Fact]
    public void Deserializar_LeeElEjemploDelDiseno()
    {
        string json = """
            {
              "idMensaje": "LIB-000187",
              "tipo": "SolicitudReserva",
              "sedeOrigen": "LIB",
              "sedeDestino": "SJO",
              "fechaHora": "2026-11-20T10:42:13-06:00",
              "contenido": {
                "idReserva": "LIB-0042-SJO",
                "idPedido": "LIB-0042",
                "lineas": [ { "codigo": "P040", "cantidad": 6 } ]
              }
            }
            """;

        Sobre sobre = SerializadorSobre.Deserializar(json);

        Assert.Equal("LIB-000187", sobre.IdMensaje);
        Assert.Equal(TipoMensaje.SolicitudReserva, sobre.Tipo);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 10, 42, 13, TimeSpan.FromHours(-6)), sobre.FechaHora);
        var c = Assert.IsType<SolicitudReserva>(sobre.Contenido);
        Assert.Equal("P040", c.Lineas[0].Codigo);
    }

    // ---------- Mensajes mal formados: siempre FormatException ----------

    [Theory]
    [InlineData("")]
    [InlineData("esto no es json")]
    [InlineData("[1, 2, 3]")]
    // falta "tipo"
    [InlineData("""{"idMensaje":"LIB-1","sedeOrigen":"LIB","sedeDestino":"SJO","fechaHora":"2026-11-20T10:42:13-06:00","contenido":{}}""")]
    // tipo desconocido
    [InlineData("""{"idMensaje":"LIB-1","tipo":"Saludo","sedeOrigen":"LIB","sedeDestino":"SJO","fechaHora":"2026-11-20T10:42:13-06:00","contenido":{}}""")]
    // tipo como número
    [InlineData("""{"idMensaje":"LIB-1","tipo":"2","sedeOrigen":"LIB","sedeDestino":"SJO","fechaHora":"2026-11-20T10:42:13-06:00","contenido":{}}""")]
    // falta "contenido"
    [InlineData("""{"idMensaje":"LIB-1","tipo":"ConfirmarReserva","sedeOrigen":"LIB","sedeDestino":"SJO","fechaHora":"2026-11-20T10:42:13-06:00"}""")]
    // fecha inválida
    [InlineData("""{"idMensaje":"LIB-1","tipo":"ConfirmarReserva","sedeOrigen":"LIB","sedeDestino":"SJO","fechaHora":"ayer","contenido":{}}""")]
    // id vacío (lo rechaza el constructor del Sobre)
    [InlineData("""{"idMensaje":"","tipo":"ConfirmarReserva","sedeOrigen":"LIB","sedeDestino":"SJO","fechaHora":"2026-11-20T10:42:13-06:00","contenido":{}}""")]
    // un campo del contenido con el tipo equivocado
    [InlineData("""{"idMensaje":"LIB-1","tipo":"RespuestaReserva","sedeOrigen":"LIB","sedeDestino":"SJO","fechaHora":"2026-11-20T10:42:13-06:00","contenido":{"aceptada":"quizás"}}""")]
    public void Deserializar_MensajeMalFormado_LanzaFormatException(string json)
    {
        Assert.Throws<FormatException>(() => SerializadorSobre.Deserializar(json));
    }
}
