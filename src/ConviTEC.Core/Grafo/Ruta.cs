namespace ConviTEC.Core.Grafo;


public class Ruta
{

    public string Id { get; set; } = "";

    public string Origen { get; set; } = "";

    public string Destino { get; set; } = "";


    public int Tiempo { get; set; }

    public Ruta() { }

    public Ruta(string id, string origen, string destino, int tiempo)
    {
        Id = id;
        Origen = origen;
        Destino = destino;
        Tiempo = tiempo;
    }
}


public class ArchivoRutas
{
    public Ruta[] Rutas { get; set; } = new Ruta[0];
}