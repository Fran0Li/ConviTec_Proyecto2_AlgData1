using System;
using ConviTEC.Core.Algoritmos;
using ConviTEC.Core.Grafo;

namespace ConviTEC.Core.Servicios;

// Resultado de validar si una sede puede entregar en un destino
public class ValidacionRuta
{
    public bool Valida { get; set; }
    public string Mensaje { get; set; } = "";
    public int Tiempo { get; set; } = ResultadoDijkstra.Inalcanzable;
    public string[] Camino { get; set; } = new string[0];
}

// Consultas de rutas para el registro de pedidos (C) y la API (D)
public class ServicioRutas
{
    private readonly GrafoDistribucion grafo;

    public ServicioRutas(GrafoDistribucion grafo)
    {
        this.grafo = grafo ?? throw new ArgumentNullException(nameof(grafo));
    }

    // No lanza excepciones: siempre devuelve un mensaje para el dependiente
    public ValidacionRuta ValidarDestino(string ubicacionSede, string destino)
    {
        var resultado = new ValidacionRuta();

        if (string.IsNullOrWhiteSpace(destino))
        {
            resultado.Mensaje = "Debe indicar un punto de entrega.";
            return resultado;
        }
        if (!grafo.ExisteUbicacion(ubicacionSede))
        {
            resultado.Mensaje = "La sede '" + ubicacionSede + "' no está en la red de distribución.";
            return resultado;
        }
        if (!grafo.ExisteUbicacion(destino))
        {
            resultado.Mensaje = "El destino '" + destino + "' no existe en la red de distribución.";
            return resultado;
        }

        string sede = grafo.NombreDe(grafo.IndiceDe(ubicacionSede));
        string llegada = grafo.NombreDe(grafo.IndiceDe(destino));

        if (sede == llegada)
        {
            resultado.Mensaje = "El destino es la misma sede (" + sede + "); no hace falta entrega.";
            return resultado;
        }

        ResultadoDijkstra dijkstra = Dijkstra.Calcular(grafo, sede);

        if (!dijkstra.EsAlcanzable(llegada))
        {
            resultado.Mensaje = "No hay ruta desde " + sede + " hasta " + llegada + ". Desde "
                + sede + " solo se puede entregar en: " + Unir(DestinosAlcanzables(sede)) + ".";
            return resultado;
        }

        resultado.Valida = true;
        resultado.Tiempo = dijkstra.TiempoHasta(llegada);
        resultado.Camino = dijkstra.CaminoHasta(llegada);
        resultado.Mensaje = "Hay ruta desde " + sede + " hasta " + llegada + " (" + resultado.Tiempo + " min).";
        return resultado;
    }

    // Lugares a los que puede entregar la sede, sin incluirla a ella
    public string[] DestinosAlcanzables(string ubicacionSede)
    {
        ResultadoDijkstra dijkstra = Dijkstra.Calcular(grafo, ubicacionSede);
        int origen = grafo.IndiceDe(ubicacionSede);
        int n = grafo.CantidadUbicaciones;

        int cantidad = 0;
        for (int i = 0; i < n; i++)
        {
            if (i != origen && dijkstra.TiempoHasta(i) != ResultadoDijkstra.Inalcanzable)
                cantidad++;
        }

        string[] destinos = new string[cantidad];
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            if (i != origen && dijkstra.TiempoHasta(i) != ResultadoDijkstra.Inalcanzable)
            {
                destinos[k] = grafo.NombreDe(i);
                k++;
            }
        }
        return destinos;
    }

    private static string Unir(string[] nombres)
    {
        if (nombres.Length == 0)
            return "(ningún destino)";

        string texto = nombres[0];
        for (int i = 1; i < nombres.Length; i++)
            texto += ", " + nombres[i];
        return texto;
    }
}