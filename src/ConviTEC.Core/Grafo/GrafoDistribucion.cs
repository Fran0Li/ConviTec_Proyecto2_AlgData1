using System;

namespace ConviTEC.Core.Grafo;

public class GrafoDistribucion
{

    public const int SinRuta = 0;

    private string[] ubicaciones;
    private int[,] matriz;
    private int cantidad;

    public GrafoDistribucion() : this(16) { }

    public GrafoDistribucion(int capacidadInicial)
    {
        if (capacidadInicial < 1)
            capacidadInicial = 1;
        ubicaciones = new string[capacidadInicial];
        matriz = new int[capacidadInicial, capacidadInicial];
        cantidad = 0;
    }


    public int CantidadUbicaciones => cantidad;

  
    public int CantidadRutas
    {
        get
        {
            int total = 0;
            for (int i = 0; i < cantidad; i++)
            {
                for (int j = i + 1; j < cantidad; j++)
                {
                    if (matriz[i, j] != SinRuta)
                        total++;
                }
            }
            return total;
        }
    }


    public int AgregarUbicacion(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("La ubicación debe tener un nombre.", nameof(nombre));

        int existente = IndiceDe(nombre);
        if (existente != -1)
            return existente;

        if (cantidad == ubicaciones.Length)
            Crecer();

        ubicaciones[cantidad] = nombre.Trim();
        cantidad++;
        return cantidad - 1;
    }


    public void AgregarRuta(string origen, string destino, int tiempo)
    {
        if (tiempo <= 0)
            throw new ArgumentException("El tiempo de una ruta debe ser mayor que cero.", nameof(tiempo));

        int i = AgregarUbicacion(origen);
        int j = AgregarUbicacion(destino);

        if (i == j)
            throw new ArgumentException("Una ruta no puede empezar y terminar en la misma ubicación (" + origen + ").");

        if (matriz[i, j] == SinRuta || tiempo < matriz[i, j])
        {
            matriz[i, j] = tiempo;
            matriz[j, i] = tiempo;
        }
    }


    public int IndiceDe(string nombre)
    {
        if (nombre == null)
            return -1;
        string buscado = nombre.Trim();
        for (int i = 0; i < cantidad; i++)
        {
            if (string.Equals(ubicaciones[i], buscado, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public bool ExisteUbicacion(string nombre)
    {
        return IndiceDe(nombre) != -1;
    }

  
    public string NombreDe(int indice)
    {
        ValidarIndice(indice);
        return ubicaciones[indice];
    }

  
    public int Tiempo(int origen, int destino)
    {
        ValidarIndice(origen);
        ValidarIndice(destino);
        return matriz[origen, destino];
    }


    public bool HayRutaDirecta(string origen, string destino)
    {
        int i = IndiceDe(origen);
        int j = IndiceDe(destino);
        if (i == -1 || j == -1)
            return false;
        return matriz[i, j] != SinRuta;
    }


    public string[] Ubicaciones()
    {
        string[] copia = new string[cantidad];
        for (int i = 0; i < cantidad; i++)
            copia[i] = ubicaciones[i];
        return copia;
    }


    private void Crecer()
    {
        int nuevaCapacidad = ubicaciones.Length * 2;

        string[] nuevasUbicaciones = new string[nuevaCapacidad];
        int[,] nuevaMatriz = new int[nuevaCapacidad, nuevaCapacidad];

        for (int i = 0; i < cantidad; i++)
        {
            nuevasUbicaciones[i] = ubicaciones[i];
            for (int j = 0; j < cantidad; j++)
                nuevaMatriz[i, j] = matriz[i, j];
        }

        ubicaciones = nuevasUbicaciones;
        matriz = nuevaMatriz;
    }

    private void ValidarIndice(int indice)
    {
        if (indice < 0 || indice >= cantidad)
            throw new ArgumentOutOfRangeException(nameof(indice), "No existe una ubicación con índice " + indice + ".");
    }
}