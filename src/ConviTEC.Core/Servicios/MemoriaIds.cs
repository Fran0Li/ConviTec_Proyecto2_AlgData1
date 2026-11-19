using System;

namespace ConviTEC.Core.Servicios;

// Recuerda los últimos identificadores vistos, en un arreglo circular de tamaño fijo.
// La usa el Despachador para no procesar dos veces el mismo mensaje (diseño 5.7, RNF-10).
//
// Ejemplo con capacidad 3:   [a, b, c]  → agregar d →  [d, b, c]  (se olvida a, el más viejo)
//                             ^siguiente                  ^siguiente
//
// Por qué tamaño fijo: RabbitMQ repite un mensaje cuando no recibió su ack (por ejemplo, la sede se
// cayó justo después de procesarlo), y esa repetición llega poco después, no días más tarde.
// Basta recordar los últimos N; así la memoria no crece sin límite mientras la sede está encendida.
public class MemoriaIds
{
    private readonly string?[] ids;
    private int siguiente;   // casilla donde se guarda el próximo id
    private int cantidad;    // cuántas casillas están ocupadas (como mucho, la capacidad)

    public MemoriaIds(int capacidad)
    {
        if (capacidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacidad), "La capacidad debe ser mayor que cero.");
        ids = new string?[capacidad];
    }

    public int Capacidad => ids.Length;

    public int Cantidad => cantidad;

    // True si el id está entre los recordados. Búsqueda lineal: O(capacidad), con 1000 ids es
    // despreciable al lado de lo que tarda un mensaje en viajar por la red.
    public bool Contiene(string id)
    {
        for (int i = 0; i < cantidad; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    // Guarda el id. Devuelve false si ya estaba (no lo guarda otra vez).
    // Si la memoria está llena, pisa el id más viejo.
    public bool Agregar(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El identificador no puede estar vacío.", nameof(id));
        if (Contiene(id))
            return false;

        ids[siguiente] = id;
        siguiente = (siguiente + 1) % ids.Length;   // al llegar al final vuelve a la casilla 0
        if (cantidad < ids.Length)
            cantidad++;
        return true;
    }
}
