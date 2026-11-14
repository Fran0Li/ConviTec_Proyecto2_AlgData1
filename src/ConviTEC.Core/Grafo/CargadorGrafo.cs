using System;
using System.Text.Json;

namespace ConviTEC.Core.Grafo;


public static class CargadorGrafo
{
    private static readonly JsonSerializerOptions opciones = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };


    public static GrafoDistribucion DesdeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("El archivo de rutas está vacío.");

        Ruta[]? rutas;
        try
        {
            if (json.TrimStart().StartsWith("["))
            {
                rutas = JsonSerializer.Deserialize<Ruta[]>(json, opciones);
            }
            else
            {
                ArchivoRutas? archivo = JsonSerializer.Deserialize<ArchivoRutas>(json, opciones);
                rutas = archivo?.Rutas;
            }
        }
        catch (JsonException e)
        {
            throw new FormatException("El archivo de rutas no es un JSON válido: " + e.Message, e);
        }

        if (rutas == null || rutas.Length == 0)
            throw new FormatException("El archivo de rutas no tiene ninguna ruta.");

        return DesdeRutas(rutas);
    }

    // Arma el grafo a partir de rutas ya leidas. Revisa cada ruta y dice cual falla.
    public static GrafoDistribucion DesdeRutas(Ruta[] rutas)
    {
        if (rutas == null)
            throw new ArgumentNullException(nameof(rutas));

        GrafoDistribucion grafo = new GrafoDistribucion();
        for (int i = 0; i < rutas.Length; i++)
        {
            Ruta ruta = rutas[i];
            string nombre = ruta != null && !string.IsNullOrWhiteSpace(ruta.Id) ? ruta.Id : "número " + (i + 1);

            if (ruta == null)
                throw new FormatException("La ruta " + nombre + " está vacía.");
            if (string.IsNullOrWhiteSpace(ruta.Origen) || string.IsNullOrWhiteSpace(ruta.Destino))
                throw new FormatException("La ruta " + nombre + " no tiene origen o destino.");
            if (ruta.Tiempo <= 0)
                throw new FormatException("La ruta " + nombre + " tiene un tiempo inválido (" + ruta.Tiempo + "). Debe ser mayor que cero.");

            try
            {
                grafo.AgregarRuta(ruta.Origen, ruta.Destino, ruta.Tiempo);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("La ruta " + nombre + " no es válida: " + e.Message, e);
            }
        }
        return grafo;
    }
}