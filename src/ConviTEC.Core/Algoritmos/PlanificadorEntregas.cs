using System;
using ConviTEC.Core.Grafo;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Algoritmos;

// Planificador voraz: desde donde está el vehículo, va al destino pendiente más cercano
// (Dijkstra) y repite desde ahí hasta entregar todo lo alcanzable.
public class PlanificadorEntregas
{
    private readonly GrafoDistribucion grafo;

    public PlanificadorEntregas(GrafoDistribucion grafo)
    {
        this.grafo = grafo ?? throw new ArgumentNullException(nameof(grafo));
    }

    public Recorrido Planificar(Sede sede, Pedido[] pedidos, DateTime salida)
    {
        if (sede == null)
            throw new ArgumentNullException(nameof(sede));
        if (pedidos == null)
            throw new ArgumentNullException(nameof(pedidos));
        if (!grafo.ExisteUbicacion(sede.Ubicacion))
            throw new ArgumentException("La sede '" + sede.Ubicacion + "' no está en la red de distribución.", nameof(sede));

        int n = pedidos.Length;
        bool[] entregado = new bool[n];
        ParadaRecorrido[] paradas = new ParadaRecorrido[n];
        int cantidadParadas = 0;
        int acumulado = 0;
        string actual = grafo.NombreDe(grafo.IndiceDe(sede.Ubicacion));

        while (true)
        {
            ResultadoDijkstra dijkstra = Dijkstra.Calcular(grafo, actual);

            // Pedido pendiente más cercano. En empate gana el primero de la lista.
            int elegido = -1;
            int mejorTiempo = 0;
            for (int i = 0; i < n; i++)
            {
                if (entregado[i] || !EsPendiente(pedidos[i]) || !grafo.ExisteUbicacion(pedidos[i].Destino))
                    continue;

                int tiempo = dijkstra.TiempoHasta(pedidos[i].Destino);
                if (tiempo == ResultadoDijkstra.Inalcanzable)
                    continue;

                if (elegido == -1 || tiempo < mejorTiempo)
                {
                    elegido = i;
                    mejorTiempo = tiempo;
                }
            }

            // No queda nada alcanzable
            if (elegido == -1)
                break;

            string destino = grafo.NombreDe(grafo.IndiceDe(pedidos[elegido].Destino));
            acumulado += mejorTiempo;

            var parada = new ParadaRecorrido();
            parada.Destino = destino;
            parada.Camino = dijkstra.CaminoHasta(destino);
            parada.TiempoTramo = mejorTiempo;
            parada.TiempoAcumulado = acumulado;
            parada.IdsPedidos = EntregarTodosEn(destino, pedidos, entregado);

            paradas[cantidadParadas] = parada;
            cantidadParadas++;
            actual = destino;
        }

        var recorrido = new Recorrido();
        recorrido.CodigoSede = sede.Codigo;
        recorrido.UbicacionInicial = sede.Ubicacion;
        recorrido.FechaSalida = salida;
        recorrido.Paradas = Recortar(paradas, cantidadParadas);
        recorrido.IdsPedidosNoAlcanzables = NoEntregados(pedidos, entregado);
        return recorrido;
    }

    // Marca como entregados todos los pedidos con ese destino y devuelve sus ids
    private string[] EntregarTodosEn(string destino, Pedido[] pedidos, bool[] entregado)
    {
        int indiceDestino = grafo.IndiceDe(destino);

        int cantidad = 0;
        for (int i = 0; i < pedidos.Length; i++)
        {
            if (!entregado[i] && EsPendiente(pedidos[i]) && grafo.IndiceDe(pedidos[i].Destino) == indiceDestino)
                cantidad++;
        }

        string[] ids = new string[cantidad];
        int k = 0;
        for (int i = 0; i < pedidos.Length; i++)
        {
            if (!entregado[i] && EsPendiente(pedidos[i]) && grafo.IndiceDe(pedidos[i].Destino) == indiceDestino)
            {
                entregado[i] = true;
                ids[k] = pedidos[i].Id;
                k++;
            }
        }
        return ids;
    }

    // Pendientes que no se pudieron entregar (destino inexistente o inalcanzable)
    private static string[] NoEntregados(Pedido[] pedidos, bool[] entregado)
    {
        int cantidad = 0;
        for (int i = 0; i < pedidos.Length; i++)
        {
            if (!entregado[i] && EsPendiente(pedidos[i]))
                cantidad++;
        }

        string[] ids = new string[cantidad];
        int k = 0;
        for (int i = 0; i < pedidos.Length; i++)
        {
            if (!entregado[i] && EsPendiente(pedidos[i]))
            {
                ids[k] = pedidos[i].Id;
                k++;
            }
        }
        return ids;
    }

    private static ParadaRecorrido[] Recortar(ParadaRecorrido[] paradas, int cantidad)
    {
        ParadaRecorrido[] resultado = new ParadaRecorrido[cantidad];
        for (int i = 0; i < cantidad; i++)
            resultado[i] = paradas[i];
        return resultado;
    }

    private static bool EsPendiente(Pedido pedido)
    {
        return pedido != null && pedido.Estado == EstadoPedido.Pendiente;
    }
}