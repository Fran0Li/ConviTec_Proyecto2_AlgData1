using ConviTEC.Core.Grafo;

namespace ConviTEC.Tests.Grafo;

// Pruebas del grafo con matriz de adyacencia y de la carga desde JSON (rol B).
public class GrafoDistribucionTests
{
    // Las 15 rutas de la tabla del enunciado.
    private const string RutasEnunciado = @"{
      ""rutas"": [
        { ""id"": ""R01"", ""origen"": ""Liberia"",    ""destino"": ""Cañas"",     ""tiempo"": 65 },
        { ""id"": ""R02"", ""origen"": ""Puntarenas"", ""destino"": ""Orotina"",   ""tiempo"": 45 },
        { ""id"": ""R03"", ""origen"": ""Puntarenas"", ""destino"": ""Alajuela"",  ""tiempo"": 80 },
        { ""id"": ""R04"", ""origen"": ""Orotina"",    ""destino"": ""Alajuela"",  ""tiempo"": 55 },
        { ""id"": ""R05"", ""origen"": ""Alajuela"",   ""destino"": ""San José"",  ""tiempo"": 35 },
        { ""id"": ""R06"", ""origen"": ""Alajuela"",   ""destino"": ""Heredia"",   ""tiempo"": 20 },
        { ""id"": ""R07"", ""origen"": ""San José"",   ""destino"": ""Heredia"",   ""tiempo"": 20 },
        { ""id"": ""R08"", ""origen"": ""San José"",   ""destino"": ""Cartago"",   ""tiempo"": 45 },
        { ""id"": ""R09"", ""origen"": ""Cartago"",    ""destino"": ""Paraíso"",   ""tiempo"": 15 },
        { ""id"": ""R10"", ""origen"": ""Paraíso"",    ""destino"": ""Turrialba"", ""tiempo"": 45 },
        { ""id"": ""R11"", ""origen"": ""San José"",   ""destino"": ""Guápiles"",  ""tiempo"": 70 },
        { ""id"": ""R12"", ""origen"": ""Guápiles"",   ""destino"": ""Siquirres"", ""tiempo"": 55 },
        { ""id"": ""R13"", ""origen"": ""Siquirres"",  ""destino"": ""Limón"",     ""tiempo"": 65 },
        { ""id"": ""R14"", ""origen"": ""Turrialba"",  ""destino"": ""Limón"",     ""tiempo"": 75 },
        { ""id"": ""R15"", ""origen"": ""Guápiles"",   ""destino"": ""Turrialba"", ""tiempo"": 80 }
      ]
    }";

    // Casos normales

    [Fact]
    public void CargaLasRutasDelEnunciado()
    {
        GrafoDistribucion grafo = CargadorGrafo.DesdeJson(RutasEnunciado);

        Assert.Equal(13, grafo.CantidadUbicaciones);
        Assert.Equal(15, grafo.CantidadRutas);
    }

    [Fact]
    public void LasRutasSonBidireccionales()
    {
        GrafoDistribucion grafo = CargadorGrafo.DesdeJson(RutasEnunciado);
        int alajuela = grafo.IndiceDe("Alajuela");
        int sanJose = grafo.IndiceDe("San José");

        Assert.Equal(35, grafo.Tiempo(alajuela, sanJose));
        Assert.Equal(35, grafo.Tiempo(sanJose, alajuela));
    }

    [Fact]
    public void SinRutaDirecta_LaCeldaEsSinRuta()
    {
        GrafoDistribucion grafo = CargadorGrafo.DesdeJson(RutasEnunciado);

        Assert.False(grafo.HayRutaDirecta("Liberia", "Alajuela"));
        Assert.Equal(GrafoDistribucion.SinRuta,
            grafo.Tiempo(grafo.IndiceDe("Liberia"), grafo.IndiceDe("Alajuela")));
    }

    [Fact]
    public void BuscarIgnoraMayusculasYEspacios()
    {
        GrafoDistribucion grafo = CargadorGrafo.DesdeJson(RutasEnunciado);

        Assert.Equal(grafo.IndiceDe("San José"), grafo.IndiceDe("  san josé "));
    }

    [Fact]
    public void AceptaUnArregloSinElObjetoRutas()
    {
        string json = @"[ { ""id"": ""R01"", ""origen"": ""Liberia"", ""destino"": ""Cañas"", ""tiempo"": 65 } ]";

        GrafoDistribucion grafo = CargadorGrafo.DesdeJson(json);

        Assert.True(grafo.HayRutaDirecta("Liberia", "Cañas"));
    }

    [Fact]
    public void CreceCuandoHayMasUbicacionesQueLaCapacidad()
    {
        var grafo = new GrafoDistribucion(2);

        grafo.AgregarRuta("A", "B", 10);
        grafo.AgregarRuta("B", "C", 20);
        grafo.AgregarRuta("C", "D", 30);

        Assert.Equal(4, grafo.CantidadUbicaciones);
        Assert.Equal(10, grafo.Tiempo(grafo.IndiceDe("A"), grafo.IndiceDe("B")));
        Assert.Equal(30, grafo.Tiempo(grafo.IndiceDe("D"), grafo.IndiceDe("C")));
    }

    // Casos excepcionales

    [Fact]
    public void UbicacionRepetida_NoSeDuplica()
    {
        var grafo = new GrafoDistribucion();

        int primera = grafo.AgregarUbicacion("Heredia");
        int segunda = grafo.AgregarUbicacion("heredia");

        Assert.Equal(primera, segunda);
        Assert.Equal(1, grafo.CantidadUbicaciones);
    }

    [Fact]
    public void RutaRepetida_SeQuedaConElTiempoMenor()
    {
        var grafo = new GrafoDistribucion();

        grafo.AgregarRuta("Alajuela", "Heredia", 30);
        grafo.AgregarRuta("Heredia", "Alajuela", 20);
        grafo.AgregarRuta("Alajuela", "Heredia", 50);

        Assert.Equal(1, grafo.CantidadRutas);
        Assert.Equal(20, grafo.Tiempo(grafo.IndiceDe("Alajuela"), grafo.IndiceDe("Heredia")));
    }

    [Fact]
    public void UbicacionInexistente_DevuelveMenosUno()
    {
        GrafoDistribucion grafo = CargadorGrafo.DesdeJson(RutasEnunciado);

        Assert.Equal(-1, grafo.IndiceDe("Puerto Viejo"));
        Assert.False(grafo.HayRutaDirecta("Puerto Viejo", "Limón"));
    }

    [Fact]
    public void TiempoCeroONegativo_LanzaExcepcion()
    {
        var grafo = new GrafoDistribucion();

        Assert.Throws<ArgumentException>(() => grafo.AgregarRuta("A", "B", 0));
        Assert.Throws<ArgumentException>(() => grafo.AgregarRuta("A", "B", -5));
    }

    [Fact]
    public void RutaDeUnaUbicacionASiMisma_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => new GrafoDistribucion().AgregarRuta("Cartago", "cartago", 10));
    }

    [Fact]
    public void IndiceFueraDeRango_LanzaExcepcion()
    {
        var grafo = new GrafoDistribucion();
        grafo.AgregarRuta("A", "B", 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => grafo.Tiempo(0, 5));
    }

    [Fact]
    public void JsonMalEscrito_LanzaFormatException()
    {
        Assert.Throws<FormatException>(() => CargadorGrafo.DesdeJson("{ rutas: [ "));
    }

    [Fact]
    public void JsonVacioOSinRutas_LanzaFormatException()
    {
        Assert.Throws<FormatException>(() => CargadorGrafo.DesdeJson(""));
        Assert.Throws<FormatException>(() => CargadorGrafo.DesdeJson(@"{ ""rutas"": [] }"));
    }

    [Fact]
    public void RutaConTiempoInvalido_ElMensajeDiceCualRuta()
    {
        string json = @"{ ""rutas"": [ { ""id"": ""R07"", ""origen"": ""A"", ""destino"": ""B"", ""tiempo"": 0 } ] }";

        var error = Assert.Throws<FormatException>(() => CargadorGrafo.DesdeJson(json));

        Assert.Contains("R07", error.Message);
    }
}