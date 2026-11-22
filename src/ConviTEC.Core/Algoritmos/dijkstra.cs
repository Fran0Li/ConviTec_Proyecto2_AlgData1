using System;
using ConviTEC.Core.Grafo;

namespace ConviTEC.Core.Algoritmos;

// Dijkstra con arreglos sobre la matriz de adyacencia: O(V²).
public static class Dijkstra
{
    public static ResultadoDijkstra Calcular(GrafoDistribucion grafo, string origen)
    {
        if (grafo == null)
            throw new ArgumentNullException(nameof(grafo));

        int indice = grafo.IndiceDe(origen);
        if (indice == -1)
            throw new ArgumentException("La ubicación '" + origen + "' no existe.", nameof(origen));

        return Calcular(grafo, indice);
    }

    public static ResultadoDijkstra Calcular(GrafoDistribucion grafo, int origen)
    {
        if (grafo == null)
            throw new ArgumentNullException(nameof(grafo));
        if (origen < 0 || origen >= grafo.CantidadUbicaciones)
            throw new ArgumentOutOfRangeException(nameof(origen));

        int n = grafo.CantidadUbicaciones;
        int[] distancia = new int[n];
        int[] anterior = new int[n];
        bool[] visitado = new bool[n];

        for (int i = 0; i < n; i++)
        {
            distancia[i] = ResultadoDijkstra.Infinito;
            anterior[i] = -1;
        }
        distancia[origen] = 0;

        for (int vuelta = 0; vuelta < n; vuelta++)
        {
            // Ubicación no visitada más cercana
            int actual = -1;
            for (int i = 0; i < n; i++)
            {
                if (!visitado[i] && distancia[i] != ResultadoDijkstra.Infinito
                    && (actual == -1 || distancia[i] < distancia[actual]))
                    actual = i;
            }

            // Las que quedan no son alcanzables
            if (actual == -1)
                break;

            visitado[actual] = true;

            // ¿Se llega más rápido a las vecinas pasando por actual?
            for (int vecina = 0; vecina < n; vecina++)
            {
                int tiempo = grafo.Tiempo(actual, vecina);
                if (tiempo == GrafoDistribucion.SinRuta || visitado[vecina])
                    continue;

                if (distancia[actual] + tiempo < distancia[vecina])
                {
                    distancia[vecina] = distancia[actual] + tiempo;
                    anterior[vecina] = actual;
                }
            }
        }

        return new ResultadoDijkstra(grafo, origen, distancia, anterior);
    }
}

public class ResultadoDijkstra
{
    public const int Infinito = int.MaxValue;
    public const int Inalcanzable = -1;

    private readonly GrafoDistribucion grafo;
    private readonly int origen;
    private readonly int[] distancia;
    private readonly int[] anterior;

    internal ResultadoDijkstra(GrafoDistribucion grafo, int origen, int[] distancia, int[] anterior)
    {
        this.grafo = grafo;
        this.origen = origen;
        this.distancia = distancia;
        this.anterior = anterior;
    }

    public string Origen => grafo.NombreDe(origen);

    public bool EsAlcanzable(string destino)
    {
        return distancia[IndiceValido(destino)] != Infinito;
    }

    // Minutos mínimos, o -1 si no hay camino
    public int TiempoHasta(string destino)
    {
        return TiempoHasta(IndiceValido(destino));
    }

    public int TiempoHasta(int indice)
    {
        if (indice < 0 || indice >= distancia.Length)
            throw new ArgumentOutOfRangeException(nameof(indice));
        return distancia[indice] == Infinito ? Inalcanzable : distancia[indice];
    }

    // Camino de origen a destino, o vacío si no hay
    public string[] CaminoHasta(string destino)
    {
        int indice = IndiceValido(destino);
        if (distancia[indice] == Infinito)
            return new string[0];

        int largo = 0;
        for (int paso = indice; paso != -1; paso = anterior[paso])
            largo++;

        // Se llena al revés siguiendo "anterior"
        string[] camino = new string[largo];
        int actual = indice;
        for (int i = largo - 1; i >= 0; i--)
        {
            camino[i] = grafo.NombreDe(actual);
            actual = anterior[actual];
        }
        return camino;
    }

    private int IndiceValido(string destino)
    {
        int indice = grafo.IndiceDe(destino);
        if (indice == -1)
            throw new ArgumentException("La ubicación '" + destino + "' no existe.", nameof(destino));
        return indice;
    }
}