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

    // Recorrido completo de una salida
    public Recorrido Planificar(Sede sede, Pedido[] pedidos, DateTime salida)
    {
        if (sede == null)
            throw new ArgumentNullException(nameof(sede));
        if (pedidos == null)
            throw new ArgumentNullException(nameof(pedidos));
        if (!grafo.ExisteUbicacion(sede.Ubicacion))
            throw new ArgumentException("La sede '" + sede.Ubicacion + "' no está en la red de distribución.", nameof(sede));

        bool[] entregado = new bool[pedidos.Length];
        ParadaRecorrido[] paradas = new ParadaRecorrido[pedidos.Length];
        int cantidadParadas = 0;
        string actual = grafo.NombreDe(grafo.IndiceDe(sede.Ubicacion));
        int acumulado = 0;

        ParadaRecorrido? parada = SiguienteParada(actual, pedidos, entregado, acumulado);
        while (parada != null)
        {
            paradas[cantidadParadas] = parada;
            cantidadParadas++;
            actual = parada.Destino;
            acumulado = parada.TiempoAcumulado;
            parada = SiguienteParada(actual, pedidos, entregado, acumulado);
        }

        var recorrido = new Recorrido();
        recorrido.CodigoSede = sede.Codigo;
        recorrido.UbicacionInicial = sede.Ubicacion;
        recorrido.FechaSalida = salida;
        recorrido.Paradas = Recortar(paradas, cantidadParadas);
        recorrido.IdsPedidosNoAlcanzables = NoEntregados(pedidos, entregado);
        return recorrido;
    }

    // Un solo paso ("calcular el siguiente punto de entrega"): la próxima parada desde la
    // ubicación actual, o null si no queda nada alcanzable. TiempoAcumulado = TiempoTramo.
    public ParadaRecorrido? SiguienteEntrega(string ubicacionActual, Pedido[] pedidos)
    {
        if (pedidos == null)
            throw new ArgumentNullException(nameof(pedidos));
        if (!grafo.ExisteUbicacion(ubicacionActual))
            throw new ArgumentException("La ubicación '" + ubicacionActual + "' no está en la red de distribución.", nameof(ubicacionActual));

        string actual = grafo.NombreDe(grafo.IndiceDe(ubicacionActual));
        return SiguienteParada(actual, pedidos, new bool[pedidos.Length], 0);
    }

    // Dijkstra desde "actual" y elige el pedido pendiente más cercano.
    // En empate gana el primero de la lista. Marca como entregados los de ese destino.
    private ParadaRecorrido? SiguienteParada(string actual, Pedido[] pedidos, bool[] entregado, int acumulado)
    {
        ResultadoDijkstra dijkstra = Dijkstra.Calcular(grafo, actual);

        int elegido = -1;
        int mejorTiempo = 0;
        for (int i = 0; i < pedidos.Length; i++)
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

        if (elegido == -1)
            return null;

        string destino = grafo.NombreDe(grafo.IndiceDe(pedidos[elegido].Destino));

        var parada = new ParadaRecorrido();
        parada.Destino = destino;
        parada.Camino = dijkstra.CaminoHasta(destino);
        parada.TiempoTramo = mejorTiempo;
        parada.TiempoAcumulado = acumulado + mejorTiempo;
        parada.IdsPedidos = EntregarTodosEn(destino, pedidos, entregado);
        return parada;
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