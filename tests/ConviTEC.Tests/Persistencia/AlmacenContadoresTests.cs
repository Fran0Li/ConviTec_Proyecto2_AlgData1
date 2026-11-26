using ConviTEC.Persistencia;

namespace ConviTEC.Tests.Persistencia;

// Pruebas de los consecutivos de pedidos y ventas (rol D).
public class AlmacenContadoresTests : IDisposable
{
    private readonly string carpeta;

    public AlmacenContadoresTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "convitec-contadores-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
    }

    public void Dispose()
    {
        if (Directory.Exists(carpeta))
            Directory.Delete(carpeta, true);
    }

    private AlmacenContadores Crear()
    {
        return new AlmacenContadores(new RepositorioSedeArchivos(carpeta));
    }

    [Fact]
    public void SinArchivo_EmpiezaEnCero()
    {
        AlmacenContadores almacen = Crear();

        Assert.Equal(0, almacen.UltimoPedido);
        Assert.Equal(0, almacen.UltimaVenta);
    }

    [Fact]
    public void SiguientePedido_DaUnoDosTres()
    {
        AlmacenContadores almacen = Crear();

        Assert.Equal(1, almacen.SiguientePedido());
        Assert.Equal(2, almacen.SiguientePedido());
        Assert.Equal(3, almacen.SiguientePedido());
    }

    [Fact]
    public void PedidosYVentas_LlevanCuentasIndependientes_SinPisarseEnElArchivo()
    {
        AlmacenContadores almacen = Crear();

        almacen.SiguientePedido();
        almacen.SiguientePedido();
        almacen.SiguienteVenta();

        AlmacenContadores despues = Crear();
        Assert.Equal(2, despues.UltimoPedido);
        Assert.Equal(1, despues.UltimaVenta);
    }

    [Fact]
    public void LosNumeros_NoSeRepitenDespuesDeReiniciar()
    {
        AlmacenContadores almacen = Crear();
        almacen.SiguientePedido();
        almacen.SiguientePedido();
        almacen.SiguientePedido();

        AlmacenContadores despuesDeReiniciar = Crear();

        Assert.Equal(4, despuesDeReiniciar.SiguientePedido());
    }

    [Fact]
    public void AsegurarPedidoAlMenos_SubeElContadorPeroNuncaLoBaja()
    {
        AlmacenContadores almacen = Crear();
        almacen.SiguientePedido();
        almacen.SiguientePedido();

        almacen.AsegurarPedidoAlMenos(10);
        Assert.Equal(10, almacen.UltimoPedido);

        almacen.AsegurarPedidoAlMenos(5);
        Assert.Equal(10, almacen.UltimoPedido);
        Assert.Equal(11, almacen.SiguientePedido());
    }

    [Fact]
    public void SiFallaLaEscritura_ElContadorQuedaComoEstaba()
    {
        // La "carpeta" de datos es en realidad un archivo, así que no se puede escribir ahí.
        string archivoQueEstorba = Path.Combine(carpeta, "estorba");
        File.WriteAllText(archivoQueEstorba, "x");
        var almacen = new AlmacenContadores(new RepositorioSedeArchivos(archivoQueEstorba));

        Assert.Throws<IOException>(() => almacen.SiguientePedido());
        Assert.Throws<IOException>(() => almacen.SiguienteVenta());

        Assert.Equal(0, almacen.UltimoPedido);
        Assert.Equal(0, almacen.UltimaVenta);
    }

    [Fact]
    public void ConRepositorioNull_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AlmacenContadores(null!));
    }
}