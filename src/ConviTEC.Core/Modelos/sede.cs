using System;

namespace ConviTEC.Core.Modelos;


public class Sede
{
    // Codigo unico de la sede
    public string Codigo { get; set; } = "";

    // Nombre para mostrar
    public string Nombre { get; set; } = "";

    // Ubicacion de la sede en el grafo de rutas
    // Debe escribirse igual que en rutas.json, porque de aqui parten los recorridos.
    public string Ubicacion { get; set; } = "";

    public Sede() { }

    public Sede(string codigo, string nombre, string ubicacion)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("La sede debe tener un codigo.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(ubicacion))
            throw new ArgumentException("La sede debe tener una ubicacion en el grafo.", nameof(ubicacion));

        Codigo = codigo.Trim();
        Nombre = nombre ?? "";
        Ubicacion = ubicacion.Trim();
    }
}