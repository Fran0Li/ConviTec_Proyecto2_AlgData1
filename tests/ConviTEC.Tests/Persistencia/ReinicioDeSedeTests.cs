using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;
using ConviTEC.Persistencia;

namespace ConviTEC.Tests.Persistencia;

// PR-14: reiniciar una sede no pierde nada y un archivo dañado no deja arrancar con datos a medias.
// "Reiniciar" aquí es abrir de nuevo todos los registros sobre la misma carpeta, como hace ArranqueSede.
public class ReinicioDeSedeTests : IDisposable
{
    private static readonly DateTime Ahora = new DateTime(2026, 11, 26, 9, 0, 0);

    private readonly string carpeta;

    public ReinicioDeSedeTests()
    {
        carpeta = Path.Combine(Path.GetTempPath(), "convitec-reinicio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
    }

    public void Dispose()
    {
        if (Directory.Exists(carpeta))
            Directory.Delete(carpeta, true);
    }

    // Todo lo que una sede abre al arrancar (5.10).
    private class Sesion
    {
        public RepositorioSedeArchivos Repositorio = null!;
        public AlmacenContadores Contadores = null!;
        public RegistroPedidosArchivo Pedidos = null!;
        public RegistroReservasArchivo Reservas = null!;
        public LineaInventario[] Inventario = null!;
        public Venta[] Ventas = null!;
        public InventarioDeSede[] Global = null!;
    }

    private Sesion Abrir()
    {
        var s = new Sesion();
        s.Repositorio = new RepositorioSedeArchivos(carpeta);
        s.Inventario = s.Repositorio.LeerInventario();
        s.Reservas = new RegistroReservasArchivo(s.Repositorio.RutaDe(RepositorioSedeArchivos.ArchivoReservas));
        s.Contadores = new AlmacenContadores(s.Repositorio);
        s.Pedidos = new RegistroPedidosArchivo(s.Repositorio, s.Contadores);
        s.Ventas = s.Repositorio.LeerVentas();
        s.Global = s.Repositorio.LeerInventarioGlobal();
        return s;
    }

    private static Pedido NuevoPedido(Sesion s, string destino)
    {
        string id = "LIB-" + s.Pedidos.SiguienteNumero().ToString("D4");
        return new Pedido(id, "LIB", destino, Ahora,
            new[] { new DetallePedido("P001", "Agua mineral 600 ml", 2, 800m, "LIB") });
    }

    // Primera sesión: deja la sede con pedidos en los cuatro estados, ventas, reservas, inventario y copia global.
    private void LlenarSede()
    {
        // Estado inicial válido para que Abrir() pueda leer el inventario.
        new RepositorioSedeArchivos(carpeta).GuardarInventario(new[] { new LineaInventario("P001", 10, 0) });

        Sesion s = Abrir();

        Pedido registrado = NuevoPedido(s, "Cañas");      // LIB-0001
        s.Pedidos.Guardar(registrado);

        Pedido pendiente = NuevoPedido(s, "Liberia");     // LIB-0002
        s.Pedidos.Guardar(pendiente);
        pendiente.Confirmar();
        s.Pedidos.Guardar(pendiente);

        Pedido entregado = NuevoPedido(s, "Cañas");       // LIB-0003
        entregado.Confirmar();
        entregado.MarcarEntregado(Ahora.AddHours(1), 45);
        s.Pedidos.Guardar(entregado);

        Pedido cancelado = NuevoPedido(s, "Liberia");     // LIB-0004
        cancelado.Cancelar("Sin existencias");
        s.Pedidos.Guardar(cancelado);

        Venta venta = Venta.DesdePedido(entregado);
        s.Contadores.SiguienteVenta();
        s.Repositorio.GuardarVentas(new[] { venta });

        s.Reservas.Guardar(new Reserva("R1", "LIB-0001", "LIB", "ALA",
            new[] { new LineaReserva("P002", 3) }, Ahora, TimeSpan.FromMinutes(30)));
        var confirmada = new Reserva("R2", "LIB-0002", "LIB", "SJO",
            new[] { new LineaReserva("P003", 1) }, Ahora, TimeSpan.FromMinutes(30));
        confirmada.Confirmar();
        s.Reservas.Guardar(confirmada);

        s.Repositorio.GuardarInventario(new[] { new LineaInventario("P001", 10, 2), new LineaInventario("P002", 5, 0) });
        s.Repositorio.GuardarInventarioGlobal(new[]
        {
            new InventarioDeSede("ALA", new[] { new LineaMensaje("P001", 7) })
        });
    }

    [Fact]
    public void ReiniciarLaSede_ConservaPedidosVentasReservasEInventario()
    {
        LlenarSede();

        Sesion s = Abrir();   // la sede "se reinició"

        Assert.Equal(4, s.Pedidos.Cantidad);
        Assert.Equal(EstadoPedido.Registrado, s.Pedidos.Buscar("LIB-0001")!.Estado);
        Assert.Equal(EstadoPedido.Pendiente, s.Pedidos.Buscar("LIB-0002")!.Estado);
        Assert.Equal(EstadoPedido.Cancelado, s.Pedidos.Buscar("LIB-0004")!.Estado);
        Assert.Equal("Sin existencias", s.Pedidos.Buscar("LIB-0004")!.MotivoCancelacion);

        Pedido entregado = s.Pedidos.Buscar("LIB-0003")!;
        Assert.Equal(EstadoPedido.Entregado, entregado.Estado);
        Assert.Equal(Ahora.AddHours(1), entregado.FechaEntrega);
        Assert.Equal(45, entregado.TiempoTraslado);
        Assert.Equal(1600m, entregado.MontoTotal);

        Assert.Single(s.Pedidos.EnEspera());
        Assert.Equal("LIB-0001", s.Pedidos.EnEspera()[0].Id);
        Assert.Single(s.Pedidos.Pendientes());
        Assert.Equal("LIB-0002", s.Pedidos.Pendientes()[0].Id);

        Assert.Single(s.Ventas);
        Assert.Equal("V-LIB-0003", s.Ventas[0].Id);

        Assert.Equal(2, s.Reservas.Cantidad);
        Assert.Single(s.Reservas.Pendientes());
        Assert.Equal(EstadoReserva.Confirmada, s.Reservas.Buscar("R2")!.Estado);

        Assert.Equal(2, s.Inventario.Length);
        Assert.Equal(2, s.Inventario[0].Reservado);

        Assert.Single(s.Global);
        Assert.Equal("ALA", s.Global[0].CodigoSede);
        Assert.Equal(7, s.Global[0].Lineas[0].Cantidad);
    }

    [Fact]
    public void ReiniciarLaSede_LaNumeracionContinuaSinRepetirNumeros()
    {
        LlenarSede();

        Sesion s = Abrir();

        Assert.Equal(5, s.Pedidos.SiguienteNumero());
        Assert.Equal(2, s.Contadores.SiguienteVenta());
    }

    [Fact]
    public void ReiniciarVariasVeces_NoCambiaLosDatos()
    {
        LlenarSede();

        Sesion primera = Abrir();
        Sesion segunda = Abrir();
        Sesion tercera = Abrir();

        Assert.Equal(primera.Pedidos.Cantidad, tercera.Pedidos.Cantidad);
        Assert.Equal(segunda.Reservas.Cantidad, tercera.Reservas.Cantidad);
        Assert.Equal(primera.Contadores.UltimoPedido, tercera.Contadores.UltimoPedido);
    }

    [Theory]
    [InlineData(RepositorioSedeArchivos.ArchivoPedidos)]
    [InlineData(RepositorioSedeArchivos.ArchivoVentas)]
    [InlineData(RepositorioSedeArchivos.ArchivoReservas)]
    [InlineData(RepositorioSedeArchivos.ArchivoInventario)]
    [InlineData(RepositorioSedeArchivos.ArchivoInventarioGlobal)]
    [InlineData(RepositorioSedeArchivos.ArchivoContadores)]
    public void ArchivoDanado_NoDejaArrancar_YDiceCualEs(string archivo)
    {
        LlenarSede();
        File.WriteAllText(Path.Combine(carpeta, archivo), "{esto no es un arreglo json");

        var error = Assert.Throws<FormatException>(() => Abrir());

        Assert.Contains(archivo, error.Message);
    }

    [Fact]
    public void ArchivoDeReservasDanado_NoSeSobreescribeAlIntentarArrancar()
    {
        LlenarSede();
        string ruta = Path.Combine(carpeta, RepositorioSedeArchivos.ArchivoReservas);
        File.WriteAllText(ruta, "[ {roto");

        Assert.Throws<FormatException>(() => Abrir());

        // El archivo dañado queda igual para poder revisarlo o recuperarlo; no se pisa con datos vacíos.
        Assert.Equal("[ {roto", File.ReadAllText(ruta));
    }
}
