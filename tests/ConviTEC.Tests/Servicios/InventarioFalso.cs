using ConviTEC.Core.Contratos;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Tests.Servicios;

// Inventario de mentira para probar el servicio de reservas sin esperar el de A.
// Solo sirve en pruebas: guarda los productos en arreglos y los busca uno por uno.
// Cumple el mismo contrato que el inventario real (lo comprueban ContratoInventarioReservasTests).
public class InventarioFalso : IInventarioReservas
{
    private readonly string[] codigos;
    private readonly int[] cantidades;
    private readonly int[] reservados;
    private readonly object candado = new object();

    // Por ejemplo: new InventarioFalso(("P040", 10), ("P007", 3))
    public InventarioFalso(params (string Codigo, int Cantidad)[] productos)
    {
        codigos = new string[productos.Length];
        cantidades = new int[productos.Length];
        reservados = new int[productos.Length];
        for (int i = 0; i < productos.Length; i++)
        {
            codigos[i] = productos[i].Codigo;
            cantidades[i] = productos[i].Cantidad;
        }
    }

    // Para revisar en las pruebas cuánto hay de cada cosa.
    public int Cantidad(string codigo) => cantidades[Indice(codigo)];
    public int Reservado(string codigo) => reservados[Indice(codigo)];

    public bool Reservar(LineaReserva[] lineas, out string motivo)
    {
        lock (candado)
        {
            // Primera pasada: revisar que alcance todo, sumando líneas repetidas del mismo producto.
            for (int i = 0; i < lineas.Length; i++)
            {
                int indice = Buscar(lineas[i].CodigoProducto);
                if (indice < 0)
                {
                    motivo = "El producto " + lineas[i].CodigoProducto + " no existe";
                    return false;
                }
                int pedido = TotalDe(lineas, lineas[i].CodigoProducto);
                int disponible = cantidades[indice] - reservados[indice];
                if (pedido > disponible)
                {
                    motivo = "No hay suficientes unidades de " + lineas[i].CodigoProducto +
                             " (pide " + pedido + ", hay " + disponible + ")";
                    return false;
                }
            }

            // Segunda pasada: alcanzó todo, ahora sí se aparta.
            for (int i = 0; i < lineas.Length; i++)
                reservados[Indice(lineas[i].CodigoProducto)] += lineas[i].Cantidad;

            motivo = "";
            return true;
        }
    }

    public void Confirmar(LineaReserva[] lineas)
    {
        lock (candado)
        {
            RevisarReservado(lineas);
            for (int i = 0; i < lineas.Length; i++)
            {
                int indice = Indice(lineas[i].CodigoProducto);
                cantidades[indice] -= lineas[i].Cantidad;
                reservados[indice] -= lineas[i].Cantidad;
            }
        }
    }

    public void Liberar(LineaReserva[] lineas)
    {
        lock (candado)
        {
            RevisarReservado(lineas);
            for (int i = 0; i < lineas.Length; i++)
                reservados[Indice(lineas[i].CodigoProducto)] -= lineas[i].Cantidad;
        }
    }

    public int Disponible(string codigoProducto)
    {
        lock (candado)
        {
            int indice = Buscar(codigoProducto);
            if (indice < 0)
                throw new ArgumentException("El producto " + codigoProducto + " no existe.");
            return cantidades[indice] - reservados[indice];
        }
    }

    // Antes de confirmar o liberar: cada producto debe existir y tener reservado lo que se pide.
    private void RevisarReservado(LineaReserva[] lineas)
    {
        for (int i = 0; i < lineas.Length; i++)
        {
            int indice = Buscar(lineas[i].CodigoProducto);
            if (indice < 0)
                throw new InvalidOperationException("El producto " + lineas[i].CodigoProducto + " no existe.");
            if (TotalDe(lineas, lineas[i].CodigoProducto) > reservados[indice])
                throw new InvalidOperationException("No hay tantas unidades reservadas de " + lineas[i].CodigoProducto + ".");
        }
    }

    private static int TotalDe(LineaReserva[] lineas, string codigo)
    {
        int total = 0;
        for (int i = 0; i < lineas.Length; i++)
        {
            if (lineas[i].CodigoProducto == codigo)
                total += lineas[i].Cantidad;
        }
        return total;
    }

    private int Buscar(string codigo)
    {
        for (int i = 0; i < codigos.Length; i++)
        {
            if (codigos[i] == codigo)
                return i;
        }
        return -1;
    }

    private int Indice(string codigo)
    {
        int indice = Buscar(codigo);
        if (indice < 0)
            throw new ArgumentException("El producto " + codigo + " no existe.");
        return indice;
    }
}
