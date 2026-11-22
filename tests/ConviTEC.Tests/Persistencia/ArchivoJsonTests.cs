using ConviTEC.Persistencia;

namespace ConviTEC.Tests.Persistencia;

public enum EstadoPrueba { Registrado, Pendiente }

// Clase de prueba propia, para que estas pruebas no dependan de los modelos de otros roles.
public class DatoPrueba
{
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal Precio { get; set; }
    public DateTime Fecha { get; set; }
    public EstadoPrueba Estado { get; set; }
    public string[] Etiquetas { get; set; } = new string[0];
}

// Pruebas de lectura y escritura segura de archivos JSON (rol D).
// xUnit crea una instancia nueva por prueba, así que cada una trabaja en su propia carpeta temporal.
public class ArchivoJsonTests : IDisposable
{
    private readonly string carpeta;

    public ArchivoJsonTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "convitec-json-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
    }

    public void Dispose()
    {
        if (Directory.Exists(carpeta))
            Directory.Delete(carpeta, true);
    }

    private string Ruta(string nombre)
    {
        return Path.Combine(carpeta, nombre);
    }

    private static DatoPrueba[] DosDatos()
    {
        return new[]
        {
            new DatoPrueba
            {
                Nombre = "Café Ñandú",
                Cantidad = 3,
                Precio = 1250.50m,
                Fecha = new DateTime(2026, 11, 22, 16, 30, 0),
                Estado = EstadoPrueba.Pendiente,
                Etiquetas = new[] { "bebida", "caliente" }
            },
            new DatoPrueba { Nombre = "Galleta", Cantidad = 10, Precio = 500m }
        };
    }

    // ---------- Casos normales ----------

    [Fact]
    public void GuardarYLeer_DevuelveLosMismosDatos()
    {
        string ruta = Ruta("datos.json");

        ArchivoJson.GuardarArreglo(ruta, DosDatos());
        DatoPrueba[] leidos = ArchivoJson.LeerArreglo<DatoPrueba>(ruta);

        Assert.Equal(2, leidos.Length);
        Assert.Equal("Café Ñandú", leidos[0].Nombre);
        Assert.Equal(3, leidos[0].Cantidad);
        Assert.Equal(1250.50m, leidos[0].Precio);
        Assert.Equal(new DateTime(2026, 11, 22, 16, 30, 0), leidos[0].Fecha);
        Assert.Equal(EstadoPrueba.Pendiente, leidos[0].Estado);
        Assert.Equal(new[] { "bebida", "caliente" }, leidos[0].Etiquetas);
        Assert.Equal("Galleta", leidos[1].Nombre);
    }

    [Fact]
    public void Guardar_EscribeEnumsComoTextoYTildesSinEscapar()
    {
        string ruta = Ruta("datos.json");

        ArchivoJson.GuardarArreglo(ruta, DosDatos());
        string texto = File.ReadAllText(ruta);

        Assert.Contains("\"Pendiente\"", texto);
        Assert.Contains("Café Ñandú", texto);
    }

    [Fact]
    public void Guardar_SobrescribeElArchivoAnterior()
    {
        string ruta = Ruta("datos.json");
        ArchivoJson.GuardarArreglo(ruta, DosDatos());

        ArchivoJson.GuardarArreglo(ruta, new[] { new DatoPrueba { Nombre = "Solo uno" } });
        DatoPrueba[] leidos = ArchivoJson.LeerArreglo<DatoPrueba>(ruta);

        Assert.Single(leidos);
        Assert.Equal("Solo uno", leidos[0].Nombre);
    }

    [Fact]
    public void Guardar_NoDejaArchivoTemporal()
    {
        string ruta = Ruta("datos.json");

        ArchivoJson.GuardarArreglo(ruta, DosDatos());
        ArchivoJson.GuardarArreglo(ruta, DosDatos());

        Assert.False(File.Exists(ruta + ".tmp"));
    }

    [Fact]
    public void Guardar_CreaLaCarpetaSiNoExiste()
    {
        string ruta = Path.Combine(carpeta, "data", "LIB", "pedidos.json");

        ArchivoJson.GuardarArreglo(ruta, DosDatos());

        Assert.True(File.Exists(ruta));
    }

    [Fact]
    public void Guardar_ArregloVacio_SeLeeVacio()
    {
        string ruta = Ruta("datos.json");

        ArchivoJson.GuardarArreglo(ruta, new DatoPrueba[0]);

        Assert.Empty(ArchivoJson.LeerArreglo<DatoPrueba>(ruta));
    }

    [Fact]
    public void Leer_AceptaComentariosComasFinalesYNombresEnMinuscula()
    {
        string ruta = Ruta("a_mano.json");
        File.WriteAllText(ruta, "[\n  // producto de prueba\n  { \"nombre\": \"Agua\", \"cantidad\": 2, },\n]");

        DatoPrueba[] leidos = ArchivoJson.LeerArreglo<DatoPrueba>(ruta);

        Assert.Single(leidos);
        Assert.Equal("Agua", leidos[0].Nombre);
        Assert.Equal(2, leidos[0].Cantidad);
    }

    // ---------- Casos especiales ----------

    [Fact]
    public void Leer_ArchivoInexistente_DevuelveArregloVacio()
    {
        Assert.Empty(ArchivoJson.LeerArreglo<DatoPrueba>(Ruta("no_existe.json")));
    }

    [Fact]
    public void Leer_ArchivoVacio_DevuelveArregloVacio()
    {
        string ruta = Ruta("vacio.json");
        File.WriteAllText(ruta, "  \n ");

        Assert.Empty(ArchivoJson.LeerArreglo<DatoPrueba>(ruta));
    }

    [Fact]
    public void Leer_JsonDanado_LanzaFormatExceptionConLaRuta()
    {
        string ruta = Ruta("danado.json");
        File.WriteAllText(ruta, "[ { \"nombre\": \"Agua\" ");

        FormatException error = Assert.Throws<FormatException>(() => ArchivoJson.LeerArreglo<DatoPrueba>(ruta));

        Assert.Contains("danado.json", error.Message);
    }

    [Fact]
    public void Leer_JsonQueNoEsUnArreglo_LanzaFormatException()
    {
        string ruta = Ruta("objeto.json");
        File.WriteAllText(ruta, "{ \"nombre\": \"Agua\" }");

        Assert.Throws<FormatException>(() => ArchivoJson.LeerArreglo<DatoPrueba>(ruta));
    }

    [Fact]
    public void Leer_ConUnaEntradaNull_LanzaFormatException()
    {
        string ruta = Ruta("con_null.json");
        File.WriteAllText(ruta, "[ null ]");

        Assert.Throws<FormatException>(() => ArchivoJson.LeerArreglo<DatoPrueba>(ruta));
    }

    [Fact]
    public void Guardar_ConUnTemporalViejoDeUnaCaida_LoSobrescribe()
    {
        string ruta = Ruta("datos.json");
        ArchivoJson.GuardarArreglo(ruta, new[] { new DatoPrueba { Nombre = "Bueno" } });
        File.WriteAllText(ruta + ".tmp", "basura de una escritura interrumpida");

        ArchivoJson.GuardarArreglo(ruta, new[] { new DatoPrueba { Nombre = "Nuevo" } });

        Assert.Equal("Nuevo", ArchivoJson.LeerArreglo<DatoPrueba>(ruta)[0].Nombre);
        Assert.False(File.Exists(ruta + ".tmp"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RutaVacia_LanzaArgumentException(string ruta)
    {
        Assert.Throws<ArgumentException>(() => ArchivoJson.LeerArreglo<DatoPrueba>(ruta));
        Assert.Throws<ArgumentException>(() => ArchivoJson.GuardarArreglo(ruta, new DatoPrueba[0]));
    }

    [Fact]
    public void Guardar_ConDatosNull_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ArchivoJson.GuardarArreglo<DatoPrueba>(Ruta("x.json"), null!));
    }
}