using ConviTEC.Core.Algoritmos;
using ConviTEC.Core.Grafo;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

// Pruebas de los destinos disponibles desde una sede (rol D).
public class ServicioDestinosTests
{
    // Un grafo chico con la misma forma del enunciado: Guanacaste (Liberia-Cañas) aislado del resto.
    private static ServicioDestinos Crear()
    {
        GrafoDistribucion grafo = CargadorGrafo.DesdeRutas(new[]
        {
            new Ruta("R01", "Liberia", "Cañas", 65),
            new Ruta("R05", "Alajuela", "San José", 35),
            new Ruta("R06", "Alajuela", "Heredia", 20),
            new Ruta("R07", "San José", "Heredia", 20)
        });
        return new ServicioDestinos(grafo);
    }

    private static DestinoDisponible De(DestinoDisponible[] destinos, string nombre)
    {
        return destinos.Single(d => d.Nombre == nombre);
    }

    [Fact]
    public void DesdeAlajuela_AlcanzaElValleCentralYNoGuanacaste()
    {
        DestinoDisponible[] destinos = Crear().Listar("Alajuela");

        Assert.Equal(4, destinos.Length);
        Assert.True(De(destinos, "San José").Alcanzable);
        Assert.Equal(35, De(destinos, "San José").Tiempo);
        Assert.True(De(destinos, "Heredia").Alcanzable);
        Assert.Equal(20, De(destinos, "Heredia").Tiempo);
        Assert.False(De(destinos, "Liberia").Alcanzable);
        Assert.False(De(destinos, "Cañas").Alcanzable);
    }

    [Fact]
    public void DesdeLiberia_SoloAlcanzaCañas()
    {
        DestinoDisponible[] destinos = Crear().Listar("Liberia");

        Assert.Equal(4, destinos.Length);
        Assert.True(De(destinos, "Cañas").Alcanzable);
        Assert.Equal(65, De(destinos, "Cañas").Tiempo);
        Assert.False(De(destinos, "Alajuela").Alcanzable);
        Assert.False(De(destinos, "San José").Alcanzable);
        Assert.False(De(destinos, "Heredia").Alcanzable);
    }

    [Fact]
    public void LosNoAlcanzables_TienenTiempoInalcanzable()
    {
        DestinoDisponible[] destinos = Crear().Listar("Liberia");

        Assert.Equal(ResultadoDijkstra.Inalcanzable, De(destinos, "Heredia").Tiempo);
    }

    [Fact]
    public void LaPropiaSedeNoApareceComoDestino()
    {
        DestinoDisponible[] destinos = Crear().Listar("Alajuela");

        Assert.DoesNotContain(destinos, d => d.Nombre == "Alajuela");
    }

    [Theory]
    [InlineData("Nunca")]
    [InlineData("")]
    [InlineData("  ")]
    public void ConUbicacionDeSedeInvalida_LanzaArgumentException(string ubicacion)
    {
        Assert.Throws<ArgumentException>(() => Crear().Listar(ubicacion));
    }

    [Fact]
    public void ConGrafoNull_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ServicioDestinos(null!));
    }
}