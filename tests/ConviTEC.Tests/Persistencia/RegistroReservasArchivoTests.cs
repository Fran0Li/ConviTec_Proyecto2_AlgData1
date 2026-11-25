using ConviTEC.Core.Modelos;
using ConviTEC.Persistencia;

namespace ConviTEC.Tests.Persistencia;

// Pruebas del registro de reservas en archivo (rol D).
public class RegistroReservasArchivoTests : IDisposable
{
    private static readonly DateTime Ahora = new DateTime(2026, 11, 25, 10, 0, 0);

    private readonly string carpeta;

    public RegistroReservasArchivoTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "convitec-reservas-" + Guid.NewGuid().ToString("N"));
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

    private static Reserva Crear(string id, string producto = "P040", int cantidad = 2)
    {
        return new Reserva(id, "LIB-0001", "LIB", "SJO",
            new[] { new LineaReserva(producto, cantidad) }, Ahora, TimeSpan.FromMinutes(5));
    }

    // ---------- Casos normales ----------

    [Fact]
    public void GuardarYBuscar_DevuelveLaReserva()
    {
        var registro = new RegistroReservasArchivo(Ruta("reservas.json"));

        registro.Guardar(Crear("R1"));

        Reserva? encontrada = registro.Buscar("R1");
        Assert.NotNull(encontrada);
        Assert.Equal("R1", encontrada!.IdReserva);
        Assert.Equal(1, registro.Cantidad);
    }

    [Fact]
    public void Guardar_ConElMismoId_ReemplazaLaReserva()
    {
        var registro = new RegistroReservasArchivo(Ruta("reservas.json"));
        registro.Guardar(Crear("R1"));

        Reserva cambiada = Crear("R1");
        cambiada.Confirmar();
        registro.Guardar(cambiada);

        Assert.Equal(1, registro.Cantidad);
        Assert.Equal(EstadoReserva.Confirmada, registro.Buscar("R1")!.Estado);
    }

    [Fact]
    public void LoGuardado_SobreviveAUnReinicio()
    {
        string ruta = Ruta("reservas.json");
        var registro = new RegistroReservasArchivo(ruta);
        Reserva reserva = Crear("LIB-0001-SJO", "P007", 4);
        reserva.Confirmar();
        registro.Guardar(reserva);

        var despuesDeReiniciar = new RegistroReservasArchivo(ruta);

        Reserva? leida = despuesDeReiniciar.Buscar("LIB-0001-SJO");
        Assert.NotNull(leida);
        Assert.Equal("LIB", leida!.SedeSolicitante);
        Assert.Equal("SJO", leida.SedeDuena);
        Assert.Equal(EstadoReserva.Confirmada, leida.Estado);
        Assert.Single(leida.Lineas);
        Assert.Equal("P007", leida.Lineas[0].CodigoProducto);
        Assert.Equal(4, leida.Lineas[0].Cantidad);
        Assert.Equal(Ahora.AddMinutes(5), leida.FechaVencimiento);
    }

    [Fact]
    public void Pendientes_SoloDevuelveLasQueSiguenPendientes()
    {
        var registro = new RegistroReservasArchivo(Ruta("reservas.json"));
        Reserva confirmada = Crear("R2");
        confirmada.Confirmar();
        Reserva liberada = Crear("R3");
        liberada.Liberar();
        registro.Guardar(Crear("R1"));
        registro.Guardar(confirmada);
        registro.Guardar(liberada);

        Reserva[] pendientes = registro.Pendientes();

        Assert.Single(pendientes);
        Assert.Equal("R1", pendientes[0].IdReserva);
    }

    [Fact]
    public void Guardar_MasReservasQueLaCapacidadInicial_TodasSeEncuentranTambienTrasReiniciar()
    {
        string ruta = Ruta("reservas.json");
        var registro = new RegistroReservasArchivo(ruta);

        for (int i = 0; i < 50; i++)
            registro.Guardar(Crear("R" + i));

        var despuesDeReiniciar = new RegistroReservasArchivo(ruta);
        Assert.Equal(50, registro.Cantidad);
        Assert.Equal(50, despuesDeReiniciar.Cantidad);
        for (int i = 0; i < 50; i++)
        {
            Assert.NotNull(registro.Buscar("R" + i));
            Assert.NotNull(despuesDeReiniciar.Buscar("R" + i));
        }
    }

    // ---------- Casos especiales ----------

    [Fact]
    public void Buscar_ReservaInexistenteOIdVacio_DevuelveNull()
    {
        var registro = new RegistroReservasArchivo(Ruta("reservas.json"));
        registro.Guardar(Crear("R1"));

        Assert.Null(registro.Buscar("no-existe"));
        Assert.Null(registro.Buscar(""));
        Assert.Null(registro.Buscar("   "));
    }

    [Fact]
    public void Pendientes_SinReservas_DevuelveArregloVacio()
    {
        var registro = new RegistroReservasArchivo(Ruta("reservas.json"));

        Assert.Empty(registro.Pendientes());
    }

    [Fact]
    public void ArchivoInexistente_ArrancaVacio_YSeCreaAlGuardar()
    {
        string ruta = Path.Combine(carpeta, "data", "LIB", "reservas.json");

        var registro = new RegistroReservasArchivo(ruta);
        Assert.Equal(0, registro.Cantidad);
        Assert.False(File.Exists(ruta));

        registro.Guardar(Crear("R1"));
        Assert.True(File.Exists(ruta));
    }

    [Fact]
    public void Guardar_Null_LanzaArgumentNullException()
    {
        var registro = new RegistroReservasArchivo(Ruta("reservas.json"));

        Assert.Throws<ArgumentNullException>(() => registro.Guardar(null!));
    }

    [Fact]
    public void Guardar_SinIdReserva_LanzaArgumentException()
    {
        var registro = new RegistroReservasArchivo(Ruta("reservas.json"));

        Assert.Throws<ArgumentException>(() => registro.Guardar(new Reserva()));
        Assert.Equal(0, registro.Cantidad);
    }

    [Fact]
    public void Constructor_ConRutaVacia_LanzaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RegistroReservasArchivo(" "));
    }

    [Fact]
    public void Constructor_ConArchivoDanado_LanzaFormatException()
    {
        string ruta = Ruta("reservas.json");
        File.WriteAllText(ruta, "[ { \"idReserva\": ");

        Assert.Throws<FormatException>(() => new RegistroReservasArchivo(ruta));
    }

    [Fact]
    public void Constructor_ConReservaSinId_LanzaFormatException()
    {
        string ruta = Ruta("reservas.json");
        File.WriteAllText(ruta, "[ { \"idReserva\": \"\" } ]");

        Assert.Throws<FormatException>(() => new RegistroReservasArchivo(ruta));
    }

    [Fact]
    public void Guardar_SiFallaLaEscritura_DeshaceElCambio()
    {
        // La "carpeta" del archivo es en realidad un archivo, así que no se puede escribir ahí.
        string archivoQueEstorba = Ruta("estorba");
        File.WriteAllText(archivoQueEstorba, "x");
        var registro = new RegistroReservasArchivo(Path.Combine(archivoQueEstorba, "reservas.json"));

        Assert.Throws<IOException>(() => registro.Guardar(Crear("R1")));

        Assert.Null(registro.Buscar("R1"));
        Assert.Equal(0, registro.Cantidad);
    }
}