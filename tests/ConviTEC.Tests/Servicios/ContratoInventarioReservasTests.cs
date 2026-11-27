using ConviTEC.Core.Contratos;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Tests.Servicios;

// Pruebas del CONTRATO IInventarioReservas: lo que cualquier inventario debe cumplir para que las
// reservas en dos fases funcionen. Son abstractas: cada implementación hereda de esta clase y dice
// cómo crear su inventario. Así las mismas pruebas sirven para el inventario falso y para el real
// del rol A (con su AVL): A solo tiene que agregar una clase como InventarioFalsoCumpleElContrato.
//
// Para revisar cantidades usan solo los métodos de la interfaz (Disponible), no detalles internos.
public abstract class ContratoInventarioReservasTests
{
    // Crea un inventario con esos productos y cantidades, sin nada reservado.
    protected abstract IInventarioReservas Crear(params (string Codigo, int Cantidad)[] productos);

    private static LineaReserva[] Lineas(params (string Codigo, int Cantidad)[] lineas)
    {
        var resultado = new LineaReserva[lineas.Length];
        for (int i = 0; i < lineas.Length; i++)
            resultado[i] = new LineaReserva(lineas[i].Codigo, lineas[i].Cantidad);
        return resultado;
    }

    // ---------- Reservar (fase 1) ----------

    [Fact]
    public void Reservar_SiAlcanza_ApartaYBajaLoDisponible()
    {
        IInventarioReservas inv = Crear(("P040", 10), ("P007", 3));

        bool ok = inv.Reservar(Lineas(("P040", 6), ("P007", 3)), out string motivo);

        Assert.True(ok);
        Assert.Equal("", motivo);
        Assert.Equal(4, inv.Disponible("P040"));
        Assert.Equal(0, inv.Disponible("P007"));
    }

    [Fact]
    public void Reservar_SiUnaLineaNoAlcanza_NoApartaNinguna()
    {
        IInventarioReservas inv = Crear(("P040", 10), ("P007", 3));

        bool ok = inv.Reservar(Lineas(("P040", 6), ("P007", 4)), out string motivo);

        Assert.False(ok);
        Assert.Contains("P007", motivo);
        // Todo o nada: P040 tampoco se apartó.
        Assert.Equal(10, inv.Disponible("P040"));
        Assert.Equal(3, inv.Disponible("P007"));
    }

    [Fact]
    public void Reservar_ProductoInexistente_NoApartaNada()
    {
        IInventarioReservas inv = Crear(("P040", 10));

        bool ok = inv.Reservar(Lineas(("P040", 1), ("P999", 1)), out string motivo);

        Assert.False(ok);
        Assert.Contains("P999", motivo);
        Assert.Equal(10, inv.Disponible("P040"));
    }

    [Fact]
    public void Reservar_LineasDelMismoProducto_SeSuman()
    {
        IInventarioReservas inv = Crear(("P040", 10));

        // 6 + 6 = 12 > 10: no alcanza aunque cada línea sola sí.
        Assert.False(inv.Reservar(Lineas(("P040", 6), ("P040", 6)), out _));
        Assert.True(inv.Reservar(Lineas(("P040", 4), ("P040", 6)), out _));
        Assert.Equal(0, inv.Disponible("P040"));
    }

    [Fact]
    public void Reservar_LoYaApartadoNoSePuedeVolverAApartar()
    {
        IInventarioReservas inv = Crear(("P040", 10));
        inv.Reservar(Lineas(("P040", 8)), out _);

        bool ok = inv.Reservar(Lineas(("P040", 3)), out string motivo);

        Assert.False(ok);
        Assert.Contains("P040", motivo);
    }

    // ---------- Confirmar y liberar (fase 2) ----------

    [Fact]
    public void Confirmar_DescuentaLoReservado()
    {
        IInventarioReservas inv = Crear(("P040", 10));
        inv.Reservar(Lineas(("P040", 6)), out _);

        inv.Confirmar(Lineas(("P040", 6)));

        // Quedan 4 en total y nada apartado: se pueden reservar los 4, pero no 5.
        Assert.Equal(4, inv.Disponible("P040"));
        Assert.False(inv.Reservar(Lineas(("P040", 5)), out _));
        Assert.True(inv.Reservar(Lineas(("P040", 4)), out _));
    }

    [Fact]
    public void Liberar_DevuelveADisponible()
    {
        IInventarioReservas inv = Crear(("P040", 10));
        inv.Reservar(Lineas(("P040", 6)), out _);

        inv.Liberar(Lineas(("P040", 6)));

        Assert.Equal(10, inv.Disponible("P040"));
    }

    [Fact]
    public void Confirmar_SinReservaPrevia_LanzaExcepcion()
    {
        IInventarioReservas inv = Crear(("P040", 10));

        Assert.Throws<InvalidOperationException>(() => inv.Confirmar(Lineas(("P040", 1))));
        Assert.Equal(10, inv.Disponible("P040"));
    }

    [Fact]
    public void Liberar_MasDeLoReservado_LanzaExcepcion()
    {
        IInventarioReservas inv = Crear(("P040", 10));
        inv.Reservar(Lineas(("P040", 2)), out _);

        Assert.Throws<InvalidOperationException>(() => inv.Liberar(Lineas(("P040", 3))));
        Assert.Equal(8, inv.Disponible("P040"));
    }

    [Fact]
    public void Disponible_ProductoInexistente_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => Crear(("P040", 10)).Disponible("P999"));
    }

    // ---------- Existencias ----------

    [Fact]
    public void Existencias_DevuelveCantidadYReservadoOrdenadosPorCodigo()
    {
        IInventarioReservas inv = Crear(("P040", 10), ("P007", 3));
        inv.Reservar(Lineas(("P040", 4)), out _);

        LineaInventario[] lineas = inv.Existencias();

        Assert.Equal(2, lineas.Length);
        Assert.Equal("P007", lineas[0].Codigo);
        Assert.Equal("P040", lineas[1].Codigo);
        Assert.Equal(10, lineas[1].Cantidad);
        Assert.Equal(4, lineas[1].Reservado);
    }

    [Fact]
    public void Existencias_SinProductos_DevuelveArregloVacio()
    {
        Assert.Empty(Crear().Existencias());
    }

    // ---------- Candado ----------

    [Fact]
    public async Task Reservar_DesdeVariosHilos_NuncaApartaDeMas()
    {
        // 100 hilos piden 1 unidad cada uno y solo hay 30: exactamente 30 deben lograrlo.
        IInventarioReservas inv = Crear(("P040", 30));
        int logrados = 0;

        var tareas = new Task[100];
        for (int i = 0; i < tareas.Length; i++)
        {
            tareas[i] = Task.Run(() =>
            {
                if (inv.Reservar(Lineas(("P040", 1)), out _))
                    Interlocked.Increment(ref logrados);
            });
        }
        await Task.WhenAll(tareas);

        Assert.Equal(30, logrados);
        Assert.Equal(0, inv.Disponible("P040"));
    }
}

// El inventario falso de las pruebas cumple el contrato.
public class InventarioFalsoCumpleElContrato : ContratoInventarioReservasTests
{
    protected override IInventarioReservas Crear(params (string Codigo, int Cantidad)[] productos)
    {
        return new InventarioFalso(productos);
    }
}
