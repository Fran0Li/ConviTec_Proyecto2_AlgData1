using ConviTEC.Core.Algoritmos;
using ConviTEC.Core.Grafo;

namespace ConviTEC.Tests.Algoritmos;

public class DijkstraTests
{
    // Las 15 rutas del enunciado
    private static GrafoDistribucion Red()
    {
        return CargadorGrafo.DesdeRutas(new Ruta[]
        {
            new Ruta("R01", "Liberia", "Cañas", 65),
            new Ruta("R02", "Puntarenas", "Orotina", 45),
            new Ruta("R03", "Puntarenas", "Alajuela", 80),
            new Ruta("R04", "Orotina", "Alajuela", 55),
            new Ruta("R05", "Alajuela", "San José", 35),
            new Ruta("R06", "Alajuela", "Heredia", 20),
            new Ruta("R07", "San José", "Heredia", 20),
            new Ruta("R08", "San José", "Cartago", 45),
            new Ruta("R09", "Cartago", "Paraíso", 15),
            new Ruta("R10", "Paraíso", "Turrialba", 45),
            new Ruta("R11", "San José", "Guápiles", 70),
            new Ruta("R12", "Guápiles", "Siquirres", 55),
            new Ruta("R13", "Siquirres", "Limón", 65),
            new Ruta("R14", "Turrialba", "Limón", 75),
            new Ruta("R15", "Guápiles", "Turrialba", 80)
        });
    }

    // Casos normales

    [Fact]
    public void RutaDirecta()
    {
        var r = Dijkstra.Calcular(Red(), "Alajuela");

        Assert.Equal(20, r.TiempoHasta("Heredia"));
        Assert.Equal(new[] { "Alajuela", "Heredia" }, r.CaminoHasta("Heredia"));
    }

    [Fact]
    public void PasaPorPuntosIntermedios()
    {
        var r = Dijkstra.Calcular(Red(), "Alajuela");

        Assert.Equal(80, r.TiempoHasta("Cartago"));
        Assert.Equal(new[] { "Alajuela", "San José", "Cartago" }, r.CaminoHasta("Cartago"));
    }

    [Fact]
    public void EscogeElMasRapidoAunqueTengaMasParadas()
    {
        var r = Dijkstra.Calcular(Red(), "Alajuela");

        // Por Cartago: 215. Por Guápiles: 225.
        Assert.Equal(215, r.TiempoHasta("Limón"));
        Assert.Equal(new[] { "Alajuela", "San José", "Cartago", "Paraíso", "Turrialba", "Limón" },
            r.CaminoHasta("Limón"));
    }

    [Fact]
    public void RutaDirectaMasRapidaQueDesvio()
    {
        var r = Dijkstra.Calcular(Red(), "Puntarenas");

        Assert.Equal(80, r.TiempoHasta("Alajuela"));
        Assert.Equal(115, r.TiempoHasta("San José"));
    }

    [Fact]
    public void OrigenACeroMinutos()
    {
        var r = Dijkstra.Calcular(Red(), "Limón");

        Assert.Equal(0, r.TiempoHasta("Limón"));
        Assert.Equal(new[] { "Limón" }, r.CaminoHasta("Limón"));
    }

    [Fact]
    public void CambiaSegunElOrigen()
    {
        var red = Red();

        Assert.Equal(35, Dijkstra.Calcular(red, "Alajuela").TiempoHasta("San José"));
        Assert.Equal(20, Dijkstra.Calcular(red, "Heredia").TiempoHasta("San José"));
        Assert.Equal(45, Dijkstra.Calcular(red, "Cartago").TiempoHasta("San José"));
    }

    [Fact]
    public void IgnoraMayusculas()
    {
        Assert.Equal(80, Dijkstra.Calcular(Red(), " alajuela ").TiempoHasta("CARTAGO"));
    }

    // Casos excepcionales

    [Fact]
    public void LiberiaSoloLlegaACanas()
    {
        var r = Dijkstra.Calcular(Red(), "Liberia");

        Assert.Equal(65, r.TiempoHasta("Cañas"));
        Assert.False(r.EsAlcanzable("San José"));
        Assert.Equal(ResultadoDijkstra.Inalcanzable, r.TiempoHasta("San José"));
        Assert.Empty(r.CaminoHasta("San José"));
    }

    [Fact]
    public void SanJoseNoLlegaAGuanacaste()
    {
        var r = Dijkstra.Calcular(Red(), "San José");

        Assert.False(r.EsAlcanzable("Liberia"));
        Assert.False(r.EsAlcanzable("Cañas"));
    }

    [Fact]
    public void OrigenInexistente()
    {
        Assert.Throws<ArgumentException>(() => Dijkstra.Calcular(Red(), "Puerto Viejo"));
    }

    [Fact]
    public void DestinoInexistente()
    {
        var r = Dijkstra.Calcular(Red(), "Alajuela");

        Assert.Throws<ArgumentException>(() => r.TiempoHasta("Puerto Viejo"));
        Assert.Throws<ArgumentException>(() => r.CaminoHasta("Puerto Viejo"));
    }

    [Fact]
    public void GrafoNulo()
    {
        Assert.Throws<ArgumentNullException>(() => Dijkstra.Calcular(null!, "Alajuela"));
    }

    [Fact]
    public void IndiceFueraDeRango()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Dijkstra.Calcular(Red(), 99));
    }

    [Fact]
    public void UnaSolaUbicacion()
    {
        var grafo = new GrafoDistribucion();
        grafo.AgregarUbicacion("Cañas");

        Assert.Equal(0, Dijkstra.Calcular(grafo, "Cañas").TiempoHasta("Cañas"));
    }
}