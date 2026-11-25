using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;
using ConviTEC.Persistencia;

namespace ConviTEC.Tests.Persistencia;

// Pruebas del repositorio de archivos de una sede (rol D): lectura, guardado, reinicio y archivos dañados.
public class RepositorioSedeArchivosTests : IDisposable
{
    private readonly string carpeta;

    public RepositorioSedeArchivosTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "convitec-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
    }

    public void Dispose()
    {
        if (Directory.Exists(carpeta))
            Directory.Delete(carpeta, true);
    }

    private RepositorioSedeArchivos Crear()
    {
        return new RepositorioSedeArchivos(carpeta);
    }

    private void Escribir(string archivo, string contenido)
    {
        File.WriteAllText(Path.Combine(carpeta, archivo), contenido);
    }

    private static Pedido PedidoEntregado()
    {
        var pedido = new Pedido("LIB-0001", "LIB", "Cañas", new DateTime(2026, 11, 25, 9, 0, 0),
            new[]
            {
                new DetallePedido("P001", "Agua mineral 600 ml", 2, 800m, "LIB"),
                new DetallePedido("P010", "Queso fresco 250 g", 1, 2800m, "SJO")
            });
        pedido.Confirmar();
        pedido.MarcarEntregado(new DateTime(2026, 11, 25, 12, 5, 0), 65);
        return pedido;
    }

    // ---------- Rutas y productos ----------

    [Fact]
    public void LeerRutas_LeeLasRutasDelArchivo()
    {
        Escribir("rutas.json", "[ { \"id\": \"R01\", \"origen\": \"Liberia\", \"destino\": \"Cañas\", \"tiempo\": 65 } ]");

        var rutas = Crear().LeerRutas();

        Assert.Single(rutas);
        Assert.Equal("Liberia", rutas[0].Origen);
        Assert.Equal("Cañas", rutas[0].Destino);
        Assert.Equal(65, rutas[0].Tiempo);
    }

    [Fact]
    public void LeerRutas_SinArchivo_LanzaFileNotFoundConLaRuta()
    {
        var error = Assert.Throws<FileNotFoundException>(() => Crear().LeerRutas());

        Assert.Contains("rutas.json", error.Message);
    }

    [Fact]
    public void LeerRutas_ConArregloVacio_LanzaFormatException()
    {
        Escribir("rutas.json", "[]");

        Assert.Throws<FormatException>(() => Crear().LeerRutas());
    }

    [Fact]
    public void LeerProductos_LeeElCatalogoConTildes()
    {
        Escribir("productos.json",
            "[ { \"codigo\": \"P004\", \"nombre\": \"Café frío en lata\", \"descripcion\": \"x\", \"categoria\": \"Bebidas\", \"precio\": 1500 } ]");

        var productos = Crear().LeerProductos();

        Assert.Single(productos);
        Assert.Equal("P004", productos[0].Codigo);
        Assert.Equal("Café frío en lata", productos[0].Nombre);
        Assert.Equal(1500m, productos[0].Precio);
    }

    [Fact]
    public void LeerProductos_SinArchivo_LanzaFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => Crear().LeerProductos());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[ { \"codigo\": \"\", \"nombre\": \"Agua\", \"precio\": 800 } ]")]
    [InlineData("[ { \"codigo\": \"P001\", \"nombre\": \" \", \"precio\": 800 } ]")]
    [InlineData("[ { \"codigo\": \"P001\", \"nombre\": \"Agua\", \"precio\": -5 } ]")]
    public void LeerProductos_ConDatosImposibles_LanzaFormatException(string contenido)
    {
        Escribir("productos.json", contenido);

        Assert.Throws<FormatException>(() => Crear().LeerProductos());
    }

    // ---------- Inventario ----------

    [Fact]
    public void LeerInventario_SinInventarioNiInicial_DevuelveVacio()
    {
        Assert.Empty(Crear().LeerInventario());
    }

    [Fact]
    public void LeerInventario_PrimerArranque_UsaElInventarioInicial()
    {
        Escribir("inventario.inicial.json", "[ { \"codigo\": \"P001\", \"cantidad\": 40, \"reservado\": 0 } ]");

        var lineas = Crear().LeerInventario();

        Assert.Single(lineas);
        Assert.Equal("P001", lineas[0].Codigo);
        Assert.Equal(40, lineas[0].Cantidad);
    }

    [Fact]
    public void GuardarInventario_DespuesSeLeeEseYNoElInicial()
    {
        Escribir("inventario.inicial.json", "[ { \"codigo\": \"P001\", \"cantidad\": 40, \"reservado\": 0 } ]");
        var repositorio = Crear();

        repositorio.GuardarInventario(new[] { new LineaInventario("P001", 35, 5) });
        var lineas = Crear().LeerInventario();   // un repositorio nuevo, como tras reiniciar la sede

        Assert.Single(lineas);
        Assert.Equal(35, lineas[0].Cantidad);
        Assert.Equal(5, lineas[0].Reservado);
    }

    [Theory]
    [InlineData("[ { \"codigo\": \"\", \"cantidad\": 1, \"reservado\": 0 } ]")]
    [InlineData("[ { \"codigo\": \"P001\", \"cantidad\": -1, \"reservado\": 0 } ]")]
    [InlineData("[ { \"codigo\": \"P001\", \"cantidad\": 5, \"reservado\": -1 } ]")]
    [InlineData("[ { \"codigo\": \"P001\", \"cantidad\": 5, \"reservado\": 6 } ]")]
    public void LeerInventario_ConDatosImposibles_LanzaFormatException(string contenido)
    {
        Escribir("inventario.json", contenido);

        Assert.Throws<FormatException>(() => Crear().LeerInventario());
    }

    // ---------- Pedidos y ventas: cerrar y reiniciar la sede (PR-14) ----------

    [Fact]
    public void Pedidos_SinArchivo_DevuelveVacio()
    {
        Assert.Empty(Crear().LeerPedidos());
    }

    [Fact]
    public void Pedidos_SobrevivenAUnReinicio()
    {
        Crear().GuardarPedidos(new[] { PedidoEntregado() });

        var leidos = Crear().LeerPedidos();

        Assert.Single(leidos);
        Pedido pedido = leidos[0];
        Assert.Equal("LIB-0001", pedido.Id);
        Assert.Equal("Cañas", pedido.Destino);
        Assert.Equal(EstadoPedido.Entregado, pedido.Estado);
        Assert.Equal(new DateTime(2026, 11, 25, 12, 5, 0), pedido.FechaEntrega);
        Assert.Equal(65, pedido.TiempoTraslado);
        Assert.Equal(2, pedido.Detalles.Length);
        Assert.Equal("SJO", pedido.Detalles[1].CodigoSedeOrigen);
        Assert.Equal(4400m, pedido.MontoTotal);
    }

    [Fact]
    public void Ventas_SobrevivenAUnReinicio()
    {
        Venta venta = Venta.DesdePedido(PedidoEntregado());
        Crear().GuardarVentas(new[] { venta });

        var leidas = Crear().LeerVentas();

        Assert.Single(leidas);
        Assert.Equal("V-LIB-0001", leidas[0].Id);
        Assert.Equal("LIB-0001", leidas[0].IdPedido);
        Assert.Equal(2, leidas[0].Detalles.Length);
        Assert.Equal(4400m, leidas[0].MontoTotal);
        Assert.Equal(65, leidas[0].TiempoTraslado);
    }

    [Fact]
    public void Pedidos_ConArchivoDanado_LanzaFormatExceptionConElNombre()
    {
        Escribir("pedidos.json", "[ { \"id\": \"LIB-0001\" ");

        var error = Assert.Throws<FormatException>(() => Crear().LeerPedidos());

        Assert.Contains("pedidos.json", error.Message);
    }

    [Theory]
    [InlineData("[ { \"id\": \"\", \"detalles\": [] } ]")]
    [InlineData("[ { \"id\": \"LIB-0001\", \"detalles\": null } ]")]
    [InlineData("[ { \"id\": \"LIB-0001\", \"detalles\": [ null ] } ]")]
    public void Pedidos_ConDatosImposibles_LanzaFormatException(string contenido)
    {
        Escribir("pedidos.json", contenido);

        Assert.Throws<FormatException>(() => Crear().LeerPedidos());
    }

    [Fact]
    public void Ventas_SinId_LanzaFormatException()
    {
        Escribir("ventas.json", "[ { \"id\": \"\", \"detalles\": [] } ]");

        Assert.Throws<FormatException>(() => Crear().LeerVentas());
    }

    // ---------- Inventario global y contadores ----------

    [Fact]
    public void InventarioGlobal_IdaYVuelta()
    {
        Crear().GuardarInventarioGlobal(new[]
        {
            new InventarioDeSede("SJO", new[] { new LineaMensaje("P001", 60), new LineaMensaje("P010", 18) })
        });

        var leida = Crear().LeerInventarioGlobal();

        Assert.Single(leida);
        Assert.Equal("SJO", leida[0].CodigoSede);
        Assert.Equal(2, leida[0].Lineas.Length);
        Assert.Equal("P010", leida[0].Lineas[1].Codigo);
        Assert.Equal(18, leida[0].Lineas[1].Cantidad);
    }

    [Fact]
    public void InventarioGlobal_SinArchivo_DevuelveVacio()
    {
        Assert.Empty(Crear().LeerInventarioGlobal());
    }

    [Fact]
    public void InventarioGlobal_ConSedeSinCodigo_LanzaFormatException()
    {
        Escribir("inventario_global.json", "[ { \"codigoSede\": \"\", \"lineas\": [] } ]");

        Assert.Throws<FormatException>(() => Crear().LeerInventarioGlobal());
    }

    [Fact]
    public void Contadores_SinArchivo_DevuelveCeros()
    {
        Contadores contadores = Crear().LeerContadores();

        Assert.Equal(0, contadores.UltimoPedido);
        Assert.Equal(0, contadores.UltimaVenta);
    }

    [Fact]
    public void Contadores_IdaYVuelta()
    {
        Crear().GuardarContadores(new Contadores { UltimoPedido = 42, UltimaVenta = 17 });

        Contadores leidos = Crear().LeerContadores();

        Assert.Equal(42, leidos.UltimoPedido);
        Assert.Equal(17, leidos.UltimaVenta);
    }

    [Fact]
    public void Contadores_Negativos_LanzaFormatException()
    {
        Escribir("contadores.json", "[ { \"ultimoPedido\": -1, \"ultimaVenta\": 0 } ]");

        Assert.Throws<FormatException>(() => Crear().LeerContadores());
    }

    [Fact]
    public void GuardarContadores_Null_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Crear().GuardarContadores(null!));
    }

    // ---------- Constructor y carpeta ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ConCarpetaVacia_LanzaArgumentException(string vacia)
    {
        Assert.Throws<ArgumentException>(() => new RepositorioSedeArchivos(vacia));
    }

    [Fact]
    public void Guardar_CreaLaCarpetaDeLaSedeSiNoExiste()
    {
        string nueva = Path.Combine(carpeta, "data", "LIM");
        var repositorio = new RepositorioSedeArchivos(nueva);

        repositorio.GuardarContadores(new Contadores { UltimoPedido = 1 });

        Assert.True(File.Exists(Path.Combine(nueva, "contadores.json")));
    }

    [Fact]
    public void LosArchivosDeUnaSedeNoSeMezclanConLosDeOtra()
    {
        var lib = new RepositorioSedeArchivos(Path.Combine(carpeta, "LIB"));
        var sjo = new RepositorioSedeArchivos(Path.Combine(carpeta, "SJO"));

        lib.GuardarContadores(new Contadores { UltimoPedido = 7 });

        Assert.Equal(7, lib.LeerContadores().UltimoPedido);
        Assert.Equal(0, sjo.LeerContadores().UltimoPedido);
    }
}