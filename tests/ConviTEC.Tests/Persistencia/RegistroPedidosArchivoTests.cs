using ConviTEC.Core.Modelos;
using ConviTEC.Persistencia;

namespace ConviTEC.Tests.Persistencia;

// Pruebas del registro de pedidos en archivo (rol D): guardado, reinicio, numeración y errores.
public class RegistroPedidosArchivoTests : IDisposable
{
    private static readonly DateTime Ahora = new DateTime(2026, 11, 25, 10, 0, 0);

    private readonly string carpeta;

    public RegistroPedidosArchivoTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "convitec-pedidos-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
    }

    public void Dispose()
    {
        if (Directory.Exists(carpeta))
            Directory.Delete(carpeta, true);
    }

    // Crea un registro nuevo sobre la misma carpeta, como al abrir de nuevo la sede.
    private RegistroPedidosArchivo Crear(string? carpetaDatos = null)
    {
        var repositorio = new RepositorioSedeArchivos(carpetaDatos ?? carpeta);
        return new RegistroPedidosArchivo(repositorio, new AlmacenContadores(repositorio));
    }

    private static Pedido Nuevo(string id)
    {
        return new Pedido(id, "LIB", "Cañas", Ahora,
            new[] { new DetallePedido("P001", "Agua mineral 600 ml", 2, 800m, "LIB") });
    }

    // ---------- Numeración ----------

    [Fact]
    public void SiguienteNumero_EmpiezaEnUnoYSube()
    {
        RegistroPedidosArchivo registro = Crear();

        Assert.Equal(1, registro.SiguienteNumero());
        Assert.Equal(2, registro.SiguienteNumero());
    }

    [Fact]
    public void SiguienteNumero_NoSeRepiteDespuesDeReiniciar()
    {
        RegistroPedidosArchivo registro = Crear();
        registro.SiguienteNumero();
        registro.SiguienteNumero();

        RegistroPedidosArchivo despuesDeReiniciar = Crear();

        Assert.Equal(3, despuesDeReiniciar.SiguienteNumero());
    }

    [Fact]
    public void SiSePerdioElContador_ElNumeroSigueDespuesDelPedidoMasAlto()
    {
        new RepositorioSedeArchivos(carpeta).GuardarPedidos(new[] { Nuevo("LIB-0003"), Nuevo("LIB-0007") });

        RegistroPedidosArchivo registro = Crear();   // no hay contadores.json

        Assert.Equal(8, registro.SiguienteNumero());
    }

    [Fact]
    public void IdsConOtraForma_NoRompenLaNumeracion()
    {
        new RepositorioSedeArchivos(carpeta).GuardarPedidos(new[] { Nuevo("ABC"), Nuevo("LIB-"), Nuevo("LIB-xyz") });

        RegistroPedidosArchivo registro = Crear();

        Assert.Equal(1, registro.SiguienteNumero());
        Assert.Equal(3, registro.Cantidad);
    }

    // ---------- Guardar y buscar ----------

    [Fact]
    public void GuardarYBuscar_DevuelveElPedido()
    {
        RegistroPedidosArchivo registro = Crear();

        registro.Guardar(Nuevo("LIB-0001"));

        Pedido? encontrado = registro.Buscar("LIB-0001");
        Assert.NotNull(encontrado);
        Assert.Equal("Cañas", encontrado!.Destino);
        Assert.Equal(1, registro.Cantidad);
    }

    [Fact]
    public void Guardar_ConElMismoId_ReemplazaElPedido()
    {
        RegistroPedidosArchivo registro = Crear();
        registro.Guardar(Nuevo("LIB-0001"));

        Pedido cambiado = Nuevo("LIB-0001");
        cambiado.Confirmar();
        registro.Guardar(cambiado);

        Assert.Equal(1, registro.Cantidad);
        Assert.Equal(EstadoPedido.Pendiente, registro.Buscar("LIB-0001")!.Estado);
    }

    [Fact]
    public void LosPedidos_SobrevivenAUnReinicio()
    {
        RegistroPedidosArchivo registro = Crear();
        Pedido pedido = Nuevo("LIB-0001");
        pedido.Confirmar();
        registro.Guardar(pedido);

        RegistroPedidosArchivo despuesDeReiniciar = Crear();

        Pedido? leido = despuesDeReiniciar.Buscar("LIB-0001");
        Assert.NotNull(leido);
        Assert.Equal(EstadoPedido.Pendiente, leido!.Estado);
        Assert.Equal("Cañas", leido.Destino);
        Assert.Equal(Ahora, leido.FechaCreacion);
        Assert.Single(leido.Detalles);
        Assert.Equal("P001", leido.Detalles[0].CodigoProducto);
        Assert.Equal(1600m, leido.MontoTotal);
    }

    [Fact]
    public void Buscar_PedidoInexistenteOIdVacio_DevuelveNull()
    {
        RegistroPedidosArchivo registro = Crear();
        registro.Guardar(Nuevo("LIB-0001"));

        Assert.Null(registro.Buscar("LIB-9999"));
        Assert.Null(registro.Buscar(""));
        Assert.Null(registro.Buscar("  "));
    }

    [Fact]
    public void Guardar_MasPedidosQueLaCapacidadInicial_TodosSeEncuentranTambienTrasReiniciar()
    {
        RegistroPedidosArchivo registro = Crear();
        for (int i = 1; i <= 40; i++)
            registro.Guardar(Nuevo("LIB-" + i.ToString("D4")));

        RegistroPedidosArchivo despuesDeReiniciar = Crear();

        Assert.Equal(40, registro.Cantidad);
        Assert.Equal(40, despuesDeReiniciar.Cantidad);
        Assert.NotNull(despuesDeReiniciar.Buscar("LIB-0040"));
    }

    // ---------- Listas por estado ----------

    [Fact]
    public void EnEspera_SoloDevuelveLosRegistrados()
    {
        RegistroPedidosArchivo registro = Crear();
        Pedido pendiente = Nuevo("LIB-0002");
        pendiente.Confirmar();
        Pedido cancelado = Nuevo("LIB-0003");
        cancelado.Cancelar("sin ruta");
        registro.Guardar(Nuevo("LIB-0001"));
        registro.Guardar(pendiente);
        registro.Guardar(cancelado);

        Pedido[] enEspera = registro.EnEspera();

        Assert.Single(enEspera);
        Assert.Equal("LIB-0001", enEspera[0].Id);
    }

    [Fact]
    public void Pendientes_SoloDevuelveLosPendientes_EnOrdenDeLlegada()
    {
        RegistroPedidosArchivo registro = Crear();
        Pedido uno = Nuevo("LIB-0001");
        uno.Confirmar();
        Pedido tres = Nuevo("LIB-0003");
        tres.Confirmar();
        registro.Guardar(uno);
        registro.Guardar(Nuevo("LIB-0002"));
        registro.Guardar(tres);

        Pedido[] pendientes = registro.Pendientes();

        Assert.Equal(new[] { "LIB-0001", "LIB-0003" }, pendientes.Select(p => p.Id));
    }

    [Fact]
    public void Todos_DevuelveTodosEnOrdenDeLlegada()
    {
        RegistroPedidosArchivo registro = Crear();
        registro.Guardar(Nuevo("LIB-0001"));
        registro.Guardar(Nuevo("LIB-0002"));

        Assert.Equal(new[] { "LIB-0001", "LIB-0002" }, registro.Todos().Select(p => p.Id));
    }

    [Fact]
    public void ListasSinPedidos_DevuelvenArregloVacio()
    {
        RegistroPedidosArchivo registro = Crear();

        Assert.Empty(registro.EnEspera());
        Assert.Empty(registro.Pendientes());
        Assert.Empty(registro.Todos());
    }

    // ---------- Errores ----------

    [Fact]
    public void Guardar_Null_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Crear().Guardar(null!));
    }

    [Fact]
    public void Guardar_SinId_LanzaArgumentException()
    {
        RegistroPedidosArchivo registro = Crear();

        Assert.Throws<ArgumentException>(() => registro.Guardar(new Pedido()));
        Assert.Equal(0, registro.Cantidad);
    }

    [Fact]
    public void Constructor_ConArgumentosNull_LanzaArgumentNullException()
    {
        var repositorio = new RepositorioSedeArchivos(carpeta);

        Assert.Throws<ArgumentNullException>(() => new RegistroPedidosArchivo(null!, new AlmacenContadores(repositorio)));
        Assert.Throws<ArgumentNullException>(() => new RegistroPedidosArchivo(repositorio, null!));
    }

    [Fact]
    public void Constructor_ConArchivoDanado_LanzaFormatException()
    {
        File.WriteAllText(Path.Combine(carpeta, "pedidos.json"), "[ { \"id\": \"LIB-0001\" ");

        Assert.Throws<FormatException>(() => Crear());
    }

    [Fact]
    public void Guardar_SiFallaLaEscritura_DeshaceElCambio()
    {
        // La "carpeta" de datos es en realidad un archivo, así que no se puede escribir ahí.
        string archivoQueEstorba = Path.Combine(carpeta, "estorba");
        File.WriteAllText(archivoQueEstorba, "x");
        RegistroPedidosArchivo registro = Crear(archivoQueEstorba);

        Assert.Throws<IOException>(() => registro.Guardar(Nuevo("LIB-0001")));

        Assert.Null(registro.Buscar("LIB-0001"));
        Assert.Equal(0, registro.Cantidad);
    }
}