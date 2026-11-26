using System;
using ConviTEC.Core.Contratos;

namespace ConviTEC.Persistencia;

// Dueño de los consecutivos de la sede (contadores.json, diseño 5.10): el último número de pedido y el de venta.
// Los registros de pedidos y de ventas comparten UN solo almacén, porque los dos números viven en el mismo
// archivo: si cada uno tuviera su copia, uno borraría el avance del otro al guardar.
// Cada número entregado queda guardado en archivo antes de devolverse, así no se repite después de un reinicio.
public class AlmacenContadores
{
    private readonly object candado = new object();
    private readonly IRepositorioSede repositorio;
    private readonly Contadores contadores;

    // Lee contadores.json. Si no existe, empieza en 0. Lanza FormatException si está dañado.
    public AlmacenContadores(IRepositorioSede repositorio)
    {
        this.repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));

        Contadores leidos = repositorio.LeerContadores();
        contadores = new Contadores { UltimoPedido = leidos.UltimoPedido, UltimaVenta = leidos.UltimaVenta };
    }

    public int UltimoPedido
    {
        get
        {
            lock (candado)
            {
                return contadores.UltimoPedido;
            }
        }
    }

    public int UltimaVenta
    {
        get
        {
            lock (candado)
            {
                return contadores.UltimaVenta;
            }
        }
    }

    // El próximo número de pedido (1, 2, 3...), ya guardado en archivo.
    // Si no se puede escribir, lanza IOException y el contador queda como estaba.
    public int SiguientePedido()
    {
        lock (candado)
        {
            contadores.UltimoPedido++;
            try
            {
                repositorio.GuardarContadores(contadores);
            }
            catch
            {
                contadores.UltimoPedido--;
                throw;
            }
            return contadores.UltimoPedido;
        }
    }

    // El próximo número de venta, ya guardado en archivo. Mismo comportamiento ante errores.
    public int SiguienteVenta()
    {
        lock (candado)
        {
            contadores.UltimaVenta++;
            try
            {
                repositorio.GuardarContadores(contadores);
            }
            catch
            {
                contadores.UltimaVenta--;
                throw;
            }
            return contadores.UltimaVenta;
        }
    }

    // Sube el contador de pedidos hasta ese número si estaba más bajo. Nunca lo baja.
    // Sirve cuando contadores.json se perdió pero pedidos.json conserva pedidos con números más altos:
    // sin esto se repetirían ids y un pedido nuevo reemplazaría a uno viejo.
    public void AsegurarPedidoAlMenos(int numero)
    {
        lock (candado)
        {
            if (numero <= contadores.UltimoPedido)
                return;

            int anterior = contadores.UltimoPedido;
            contadores.UltimoPedido = numero;
            try
            {
                repositorio.GuardarContadores(contadores);
            }
            catch
            {
                contadores.UltimoPedido = anterior;
                throw;
            }
        }
    }
}