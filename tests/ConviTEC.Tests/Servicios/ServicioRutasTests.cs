using ConviTEC.Core.Grafo;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

public class ServicioRutasTests
{
    // Las 15 rutas del enunciado
    private static ServicioRutas Servicio()
    {
        return new ServicioRutas(CargadorGrafo.DesdeRutas(new Ruta[]
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
        }));
    }

    // Casos normales

    [Fact]
    public void DestinoAlcanzable_EsValido()
    {
        ValidacionRuta v = Servicio().ValidarDestino("Alajuela", "Cartago");

        Assert.True(v.Valida);
        Assert.Equal(80, v.Tiempo);
        Assert.Equal(new[] { "Alajuela", "San José", "Cartago" }, v.Camino);
    }

    [Fact]
    public void LiberiaPuedeEntregarEnCanas()
    {
        Assert.True(Servicio().ValidarDestino("Liberia", "Cañas").Valida);
    }

    [Fact]
    public void DestinosDeLiberia_SoloCanas()
    {
        Assert.Equal(new[] { "Cañas" }, Servicio().DestinosAlcanzables("Liberia"));
    }

    [Fact]
    public void DestinosDeSanJose_TodoMenosGuanacasteYElMismo()
    {
        string[] destinos = Servicio().DestinosAlcanzables("San José");

        // 13 ubicaciones - San José - Liberia - Cañas = 10
        Assert.Equal(10, destinos.Length);
        Assert.DoesNotContain("San José", destinos);
        Assert.DoesNotContain("Liberia", destinos);
        Assert.DoesNotContain("Cañas", destinos);
    }

    // Casos excepcionales

    [Fact]
    public void DestinoInalcanzable_NoEsValidoYExplicaPorQue()
    {
        ValidacionRuta v = Servicio().ValidarDestino("Liberia", "Heredia");

        Assert.False(v.Valida);
        Assert.Equal(-1, v.Tiempo);
        Assert.Empty(v.Camino);
        Assert.Contains("solo se puede entregar en: Cañas", v.Mensaje);
    }

    [Fact]
    public void DestinoInexistente_NoEsValido()
    {
        ValidacionRuta v = Servicio().ValidarDestino("Alajuela", "Puerto Viejo");

        Assert.False(v.Valida);
        Assert.Contains("no existe", v.Mensaje);
    }

    [Fact]
    public void DestinoVacio_NoEsValido()
    {
        Assert.False(Servicio().ValidarDestino("Alajuela", "").Valida);
        Assert.False(Servicio().ValidarDestino("Alajuela", null!).Valida);
    }

    [Fact]
    public void SedeInexistente_NoEsValido()
    {
        Assert.False(Servicio().ValidarDestino("Narnia", "Cartago").Valida);
    }

    [Fact]
    public void DestinoIgualALaSede_NoEsValido()
    {
        ValidacionRuta v = Servicio().ValidarDestino("Limón", "limón");

        Assert.False(v.Valida);
        Assert.Contains("misma sede", v.Mensaje);
    }

    [Fact]
    public void GrafoNulo_LanzaExcepcion()
    {
        Assert.Throws<ArgumentNullException>(() => new ServicioRutas(null!));
    }
}