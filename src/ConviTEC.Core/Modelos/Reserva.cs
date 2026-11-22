using System;

namespace ConviTEC.Core.Modelos;

// Estados de una reserva entre sedes (reserva en dos fases).
public enum EstadoReserva
{
    // Las unidades están apartadas, esperando confirmar o liberar.
    Pendiente,

    // Se descontaron del inventario.
    Confirmada,

    // Se devolvieron a disponible.
    Liberada,

    // La sede dueña no tenía existencias y no apartó nada.
    Rechazada
}

// Un producto y una cantidad dentro de una reserva.
public class LineaReserva
{
    public string CodigoProducto { get; set; } = "";

    public int Cantidad { get; set; }

    public LineaReserva() { }

    public LineaReserva(string codigoProducto, int cantidad)
    {
        if (string.IsNullOrWhiteSpace(codigoProducto))
            throw new ArgumentException("La línea debe indicar el producto.", nameof(codigoProducto));
        if (cantidad <= 0)
            throw new ArgumentException("La cantidad debe ser mayor que cero.", nameof(cantidad));
        CodigoProducto = codigoProducto.Trim();
        Cantidad = cantidad;
    }
}

// Unidades que una sede le aparta a otra para un pedido.
// La usa el rol C en el flujo de reservas; aqui solo estan los datos y las
// transiciones de estado.
public class Reserva
{
    // Por ejemplo "LIB-0042-SJO": pedido + sede dueña.
    public string IdReserva { get; set; } = "";

    public string IdPedido { get; set; } = "";

    // Sede donde se registro el pedido (quien pide la reserva).
    public string SedeSolicitante { get; set; } = "";

    // Sede que tiene los productos y los aparta.
    public string SedeDuena { get; set; } = "";

    public LineaReserva[] Lineas { get; set; } = new LineaReserva[0];

    public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;

    public DateTime FechaCreacion { get; set; }

    // Si llega esta hora y la reserva sigue pendiente, se libera sola.
    public DateTime FechaVencimiento { get; set; }

    public Reserva() { }

    public Reserva(string idReserva, string idPedido, string sedeSolicitante, string sedeDuena,
                   LineaReserva[] lineas, DateTime fechaCreacion, TimeSpan duracion)
    {
        if (string.IsNullOrWhiteSpace(idReserva))
            throw new ArgumentException("La reserva debe tener un identificador.", nameof(idReserva));
        if (lineas == null || lineas.Length == 0)
            throw new ArgumentException("La reserva debe tener al menos una linea.", nameof(lineas));
        if (duracion <= TimeSpan.Zero)
            throw new ArgumentException("La duracion de la reserva debe ser positiva.", nameof(duracion));

        IdReserva = idReserva;
        IdPedido = idPedido ?? "";
        SedeSolicitante = sedeSolicitante ?? "";
        SedeDuena = sedeDuena ?? "";
        Lineas = lineas;
        FechaCreacion = fechaCreacion;
        FechaVencimiento = fechaCreacion + duracion;
    }

    // True si sigue pendiente y ya paso su hora de vencimiento.
    public bool EstaVencida(DateTime ahora)
    {
        return Estado == EstadoReserva.Pendiente && ahora >= FechaVencimiento;
    }

    public void Confirmar()
    {
        if (Estado != EstadoReserva.Pendiente)
            throw new InvalidOperationException("Solo se confirma una reserva pendiente. Estado actual: " + Estado + ".");
        Estado = EstadoReserva.Confirmada;
    }

    // Liberar dos veces no hace nada, porque el mensaje LiberarReserva puede llegar repetido.
    public void Liberar()
    {
        if (Estado == EstadoReserva.Liberada || Estado == EstadoReserva.Rechazada)
            return;
        if (Estado == EstadoReserva.Confirmada)
            throw new InvalidOperationException("No se puede liberar una reserva ya confirmada.");
        Estado = EstadoReserva.Liberada;
    }
}