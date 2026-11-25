using System;
using ConviTEC.Core.Algoritmos;
using ConviTEC.Core.Grafo;

namespace ConviTEC.Core.Servicios;

// Un lugar al que se podría entregar un pedido, visto desde una sede.
public class DestinoDisponible
{
    // Nombre de la ubicación en el grafo, por ejemplo "Cañas".
    public string Nombre { get; set; } = "";

    // False si no hay camino desde la sede (por ejemplo, Guanacaste está aislado del resto del grafo).
    public bool Alcanzable { get; set; }

    // Minutos del camino más corto (Dijkstra). ResultadoDijkstra.Inalcanzable (-1) si no es alcanzable.
    public int Tiempo { get; set; } = ResultadoDijkstra.Inalcanzable;
}

// Alimenta GET /api/destinos (diseño 5.8, RF-09): todas las ubicaciones del grafo con su alcance desde la sede.
public class ServicioDestinos
{
    private readonly GrafoDistribucion grafo;

    public ServicioDestinos(GrafoDistribucion grafo)
    {
        this.grafo = grafo ?? throw new ArgumentNullException(nameof(grafo));
    }

    // Todas las ubicaciones del grafo menos la propia sede, porque un pedido no puede entregarse en la
    // sede que lo recibe (decisión D-06). Lanza ArgumentException si la ubicación de la sede no está en el grafo.
    public DestinoDisponible[] Listar(string ubicacionSede)
    {
        if (string.IsNullOrWhiteSpace(ubicacionSede) || !grafo.ExisteUbicacion(ubicacionSede))
            throw new ArgumentException("La sede '" + ubicacionSede + "' no está en la red de distribución.", nameof(ubicacionSede));

        string sede = grafo.NombreDe(grafo.IndiceDe(ubicacionSede));
        ResultadoDijkstra dijkstra = Dijkstra.Calcular(grafo, sede);
        string[] ubicaciones = grafo.Ubicaciones();

        DestinoDisponible[] resultado = new DestinoDisponible[ubicaciones.Length - 1];
        int k = 0;
        for (int i = 0; i < ubicaciones.Length; i++)
        {
            if (ubicaciones[i] == sede)
                continue;

            DestinoDisponible destino = new DestinoDisponible();
            destino.Nombre = ubicaciones[i];
            destino.Alcanzable = dijkstra.EsAlcanzable(ubicaciones[i]);
            if (destino.Alcanzable)
                destino.Tiempo = dijkstra.TiempoHasta(ubicaciones[i]);
            resultado[k] = destino;
            k++;
        }
        return resultado;
    }
}