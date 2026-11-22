using ConviTEC.Core.Configuracion;

namespace ConviTEC.Tests.Configuracion;

// Pruebas de la configuración por sede (rol D): código de sede, validación y ruta de la carpeta de datos.
public class ConfiguracionSedeTests
{
    private static ConfiguracionSede Valida()
    {
        return new ConfiguracionSede
        {
            Codigo = "LIB",
            Nombre = "Liberia",
            Puerto = 5101,
            CarpetaDatos = "data/LIB"
        };
    }

    // ---------- ObtenerCodigo: casos normales ----------

    [Fact]
    public void ObtenerCodigo_LeeElArgumentoSeparadoPorEspacio()
    {
        string codigo = ConfiguracionSede.ObtenerCodigo(new[] { "--sede", "LIB" }, null);

        Assert.Equal("LIB", codigo);
    }

    [Fact]
    public void ObtenerCodigo_LeeElArgumentoConIgual()
    {
        string codigo = ConfiguracionSede.ObtenerCodigo(new[] { "--sede=ALA" }, null);

        Assert.Equal("ALA", codigo);
    }

    [Fact]
    public void ObtenerCodigo_IgnoraOtrosArgumentos()
    {
        string codigo = ConfiguracionSede.ObtenerCodigo(new[] { "--urls", "http://x", "--sede", "SJO" }, null);

        Assert.Equal("SJO", codigo);
    }

    [Fact]
    public void ObtenerCodigo_PasaAMayusculasYQuitaEspacios()
    {
        string codigo = ConfiguracionSede.ObtenerCodigo(new[] { "--sede", " lim " }, null);

        Assert.Equal("LIM", codigo);
    }

    [Fact]
    public void ObtenerCodigo_UsaLaVariableDeEntornoSiNoHayArgumento()
    {
        string codigo = ConfiguracionSede.ObtenerCodigo(new string[0], "sjo");

        Assert.Equal("SJO", codigo);
    }

    [Fact]
    public void ObtenerCodigo_ElArgumentoGanaSobreLaVariableDeEntorno()
    {
        string codigo = ConfiguracionSede.ObtenerCodigo(new[] { "--sede", "LIB" }, "LIM");

        Assert.Equal("LIB", codigo);
    }

    // ---------- ObtenerCodigo: casos especiales ----------

    [Fact]
    public void ObtenerCodigo_SinArgumentoNiVariable_Falla()
    {
        Assert.Throws<ArgumentException>(() => ConfiguracionSede.ObtenerCodigo(new string[0], null));
    }

    [Fact]
    public void ObtenerCodigo_ConVariableVacia_Falla()
    {
        Assert.Throws<ArgumentException>(() => ConfiguracionSede.ObtenerCodigo(new string[0], "  "));
    }

    [Fact]
    public void ObtenerCodigo_ConSedeSinValor_Falla()
    {
        Assert.Throws<ArgumentException>(() => ConfiguracionSede.ObtenerCodigo(new[] { "--sede" }, "LIB"));
    }

    [Theory]
    [InlineData("../LIB")]
    [InlineData("LI B")]
    [InlineData("LIB;")]
    [InlineData("SAN-JOSE")]
    [InlineData("LIMÓN")]
    public void ObtenerCodigo_ConCaracteresInvalidos_Falla(string codigo)
    {
        Assert.Throws<ArgumentException>(() => ConfiguracionSede.ObtenerCodigo(new[] { "--sede", codigo }, null));
    }

    // ---------- Validar ----------

    [Fact]
    public void Validar_AceptaUnaConfiguracionCorrecta()
    {
        Valida().Validar();
    }

    [Fact]
    public void Validar_RechazaCodigoVacio()
    {
        ConfiguracionSede config = Valida();
        config.Codigo = "";

        Assert.Throws<ArgumentException>(() => config.Validar());
    }

    [Fact]
    public void Validar_RechazaCodigoEnMinusculas()
    {
        ConfiguracionSede config = Valida();
        config.Codigo = "lib";

        Assert.Throws<ArgumentException>(() => config.Validar());
    }

    [Fact]
    public void Validar_RechazaNombreVacio()
    {
        ConfiguracionSede config = Valida();
        config.Nombre = " ";

        Assert.Throws<ArgumentException>(() => config.Validar());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1023)]
    [InlineData(65536)]
    public void Validar_RechazaPuertosFueraDeRango(int puerto)
    {
        ConfiguracionSede config = Valida();
        config.Puerto = puerto;

        Assert.Throws<ArgumentException>(() => config.Validar());
    }

    [Fact]
    public void Validar_RechazaCarpetaDeDatosVacia()
    {
        ConfiguracionSede config = Valida();
        config.CarpetaDatos = "";

        Assert.Throws<ArgumentException>(() => config.Validar());
    }

    // ---------- ResolverCarpeta ----------

    [Fact]
    public void ResolverCarpeta_DejaIgualUnaRutaAbsoluta()
    {
        string absoluta = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "convitec-datos"));

        string ruta = ConfiguracionSede.ResolverCarpeta(absoluta, "cualquier-lugar");

        Assert.Equal(absoluta, ruta);
    }

    [Fact]
    public void ResolverCarpeta_BuscaLaRaizDelRepositorioHaciaArriba()
    {
        string raiz = CrearCarpetaTemporal();
        try
        {
            File.WriteAllText(Path.Combine(raiz, ConfiguracionSede.ArchivoRaiz), "");
            string profunda = Path.Combine(raiz, "src", "ConviTEC.Sede", "bin", "Debug");
            Directory.CreateDirectory(profunda);

            string ruta = ConfiguracionSede.ResolverCarpeta("data/LIB", profunda);

            Assert.Equal(Path.GetFullPath(Path.Combine(raiz, "data", "LIB")), ruta);
        }
        finally
        {
            Directory.Delete(raiz, true);
        }
    }

    [Fact]
    public void ResolverCarpeta_SinRaizDelRepositorio_Falla()
    {
        string sinRaiz = CrearCarpetaTemporal();
        try
        {
            Assert.Throws<DirectoryNotFoundException>(() => ConfiguracionSede.ResolverCarpeta("data/LIB", sinRaiz));
        }
        finally
        {
            Directory.Delete(sinRaiz, true);
        }
    }

    private static string CrearCarpetaTemporal()
    {
        string carpeta = Path.Combine(Path.GetTempPath(), "convitec-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        return carpeta;
    }
}