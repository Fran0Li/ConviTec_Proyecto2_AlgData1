using System;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Persistencia;

// Registro de las reservas que esta sede les pidió a otras (implementa IRegistroReservas, diseño 5.4 y 5.10).
//
// Las reservas viven en un arreglo en memoria para buscarlas rápido y se guardan completas en un archivo
// JSON (por ejemplo data/LIB/reservas.json) cada vez que una cambia. Al crear el registro se lee ese
// archivo, así que lo guardado sobrevive a cerrar y volver a abrir la sede (RNF-15 y RNF-16).
//
// Por ahora buscar es recorrer el arreglo. Cuando esté la tabla hash del rol A, se cambia el almacenamiento
// de adentro y el resto del sistema no se entera, porque solo conoce IRegistroReservas.
public class RegistroReservasArchivo : IRegistroReservas
{
    private const int CapacidadInicial = 8;

    // Las peticiones web y los mensajes de RabbitMQ llegan en hilos distintos.
    private readonly object candado = new object();
    private readonly string rutaArchivo;
    private Reserva[] reservas = new Reserva[CapacidadInicial];
    private int cantidad;

    // Lee el archivo si existe. Si no existe o está vacío, el registro empieza vacío.
    // Lanza FormatException si el archivo está dañado o tiene una reserva sin IdReserva.
    public RegistroReservasArchivo(string rutaArchivo)
    {
        if (string.IsNullOrWhiteSpace(rutaArchivo))
            throw new ArgumentException("Falta la ruta del archivo de reservas.", nameof(rutaArchivo));
        this.rutaArchivo = rutaArchivo;

        Reserva[] guardadas = ArchivoJson.LeerArreglo<Reserva>(rutaArchivo);
        for (int i = 0; i < guardadas.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(guardadas[i].IdReserva))
                throw new FormatException(
                    "El archivo " + rutaArchivo + " tiene una reserva sin IdReserva (posición " + i + ").");
            Poner(guardadas[i]);
        }
    }

    // Cuántas reservas hay registradas.
    public int Cantidad
    {
        get
        {
            lock (candado)
            {
                return cantidad;
            }
        }
    }

    // La reserva con ese id, o null si no existe (o si el id viene vacío).
    public Reserva? Buscar(string idReserva)
    {
        if (string.IsNullOrWhiteSpace(idReserva))
            return null;

        lock (candado)
        {
            int indice = IndiceDe(idReserva);
            return indice == -1 ? null : reservas[indice];
        }
    }

    // Agrega la reserva o reemplaza la que tenga el mismo IdReserva, y guarda el archivo antes de volver.
    // Si no se puede escribir el archivo lanza IOException y deja el registro como estaba, para que lo que
    // hay en memoria nunca se adelante a lo que hay en disco. (Si quien llama ya modificó ese mismo objeto
    // Reserva, esa modificación no se puede deshacer desde aquí.)
    public void Guardar(Reserva reserva)
    {
        if (reserva == null)
            throw new ArgumentNullException(nameof(reserva));
        if (string.IsNullOrWhiteSpace(reserva.IdReserva))
            throw new ArgumentException("La reserva debe tener un IdReserva.", nameof(reserva));

        lock (candado)
        {
            int indice = IndiceDe(reserva.IdReserva);
            Reserva? anterior = indice == -1 ? null : reservas[indice];

            Poner(reserva);
            try
            {
                ArchivoJson.GuardarArreglo(rutaArchivo, Copia());
            }
            catch
            {
                if (anterior != null)
                {
                    reservas[indice] = anterior;
                }
                else
                {
                    cantidad--;
                    reservas[cantidad] = null!;
                }
                throw;
            }
        }
    }

    // Las reservas que siguen en estado Pendiente (para liberar las vencidas). Arreglo vacío si no hay.
    public Reserva[] Pendientes()
    {
        lock (candado)
        {
            int total = 0;
            for (int i = 0; i < cantidad; i++)
            {
                if (reservas[i].Estado == EstadoReserva.Pendiente)
                    total++;
            }

            Reserva[] resultado = new Reserva[total];
            int k = 0;
            for (int i = 0; i < cantidad; i++)
            {
                if (reservas[i].Estado == EstadoReserva.Pendiente)
                {
                    resultado[k] = reservas[i];
                    k++;
                }
            }
            return resultado;
        }
    }

    // Reemplaza la reserva con ese id o la agrega al final, agrandando el arreglo si se llenó.
    private void Poner(Reserva reserva)
    {
        int indice = IndiceDe(reserva.IdReserva);
        if (indice != -1)
        {
            reservas[indice] = reserva;
            return;
        }

        if (cantidad == reservas.Length)
        {
            Reserva[] mayor = new Reserva[reservas.Length * 2];
            for (int i = 0; i < cantidad; i++)
                mayor[i] = reservas[i];
            reservas = mayor;
        }
        reservas[cantidad] = reserva;
        cantidad++;
    }

    private int IndiceDe(string idReserva)
    {
        for (int i = 0; i < cantidad; i++)
        {
            if (reservas[i].IdReserva == idReserva)
                return i;
        }
        return -1;
    }

    // Arreglo del tamaño exacto, sin las casillas vacías del final, para escribirlo en el archivo.
    private Reserva[] Copia()
    {
        Reserva[] copia = new Reserva[cantidad];
        for (int i = 0; i < cantidad; i++)
            copia[i] = reservas[i];
        return copia;
    }
}