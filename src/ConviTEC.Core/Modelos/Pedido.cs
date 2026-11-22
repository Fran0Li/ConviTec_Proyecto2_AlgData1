using System;

namespace ConviTEC.Core.Modelos;

// Un pedido registrado en una sede. Puede llevar productos de varias sedes,
// pero siempre lo entrega la sede donde se registró.
public class Pedido
{
    // Identificador único, por ejemplo "LIB-0042" (código de sede + consecutivo).
    public string Id { get; set; } = "";

    // Sede donde se registró el pedido. Es la que hace la entrega.
    public string CodigoSede { get; set; } = "";

    // Ubicación del grafo donde se entrega, por ejemplo "Cañas".
    public string Destino { get; set; } = "";

    public DateTime FechaCreacion { get; set; }

    public EstadoPedido Estado { get; set; } = EstadoPedido.Registrado;

    // Productos solicitados. Se usa un arreglo nativo (permitido por el enunciado):
    // el detalle se arma una sola vez al registrar el pedido y no cambia después.
    public DetallePedido[] Detalles { get; set; } = new DetallePedido[0];

    // Por qué se canceló. Vacío si no está cancelado.
    public string MotivoCancelacion { get; set; } = "";

    // Fecha y hora de la entrega. Null mientras no se entregue.
    public DateTime? FechaEntrega { get; set; }

    // Minutos desde la sede hasta el destino dentro del recorrido en que se entregó.
    // Es 0 mientras no se entregue.
    public int TiempoTraslado { get; set; }

    public Pedido() { }

    public Pedido(string id, string codigoSede, string destino, DateTime fechaCreacion, DetallePedido[] detalles)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("El pedido debe tener un identificador.", nameof(id));
        if (string.IsNullOrWhiteSpace(codigoSede))
            throw new ArgumentException("El pedido debe indicar la sede donde se registró.", nameof(codigoSede));
        if (string.IsNullOrWhiteSpace(destino))
            throw new ArgumentException("El pedido debe tener un destino de entrega.", nameof(destino));
        if (detalles == null || detalles.Length == 0)
            throw new ArgumentException("El pedido debe tener al menos un producto.", nameof(detalles));
        for (int i = 0; i < detalles.Length; i++)
        {
            if (detalles[i] == null)
                throw new ArgumentException("El pedido tiene una línea vacía.", nameof(detalles));
        }

        Id = id.Trim();
        CodigoSede = codigoSede.Trim();
        Destino = destino.Trim();
        FechaCreacion = fechaCreacion;
        Detalles = detalles;
    }

    // Suma de los subtotales de todas las líneas.
    public decimal MontoTotal
    {
        get
        {
            decimal total = 0;
            for (int i = 0; i < Detalles.Length; i++)
                total += Detalles[i].Subtotal;
            return total;
        }
    }

    // Monto de venta entre tiempo de traslado (colones por minuto).
    // Sirve para encontrar los peores pedidos. Es 0 si todavía no se entregó.
    public decimal Rendimiento
    {
        get
        {
            if (Estado != EstadoPedido.Entregado)
                return 0;
            return CalcularRendimiento(MontoTotal, TiempoTraslado);
        }
    }

    // Códigos de las sedes que suministran productos a este pedido, sin repetir.
    // Por ejemplo, un pedido de LIB con productos de LIB y SJO devuelve ["LIB", "SJO"].
    public string[] SedesParticipantes()
    {
        string[] temporal = new string[Detalles.Length];
        int cantidad = 0;

        for (int i = 0; i < Detalles.Length; i++)
        {
            string sede = Detalles[i].CodigoSedeOrigen;
            bool repetida = false;
            for (int j = 0; j < cantidad; j++)
            {
                if (temporal[j] == sede)
                {
                    repetida = true;
                    break;
                }
            }
            if (!repetida)
            {
                temporal[cantidad] = sede;
                cantidad++;
            }
        }

        string[] resultado = new string[cantidad];
        for (int i = 0; i < cantidad; i++)
            resultado[i] = temporal[i];
        return resultado;
    }

    // Líneas que suministra una sede en particular. Sirve para armar la reserva de esa sede.
    public DetallePedido[] DetallesDeSede(string codigoSede)
    {
        int cantidad = 0;
        for (int i = 0; i < Detalles.Length; i++)
        {
            if (Detalles[i].CodigoSedeOrigen == codigoSede)
                cantidad++;
        }

        DetallePedido[] resultado = new DetallePedido[cantidad];
        int k = 0;
        for (int i = 0; i < Detalles.Length; i++)
        {
            if (Detalles[i].CodigoSedeOrigen == codigoSede)
            {
                resultado[k] = Detalles[i];
                k++;
            }
        }
        return resultado;
    }

    // Pasa de Registrado a Pendiente cuando todas las reservas se confirmaron.
    public void Confirmar()
    {
        if (Estado != EstadoPedido.Registrado)
            throw new InvalidOperationException("Solo se puede confirmar un pedido registrado. Estado actual: " + Estado + ".");
        Estado = EstadoPedido.Pendiente;
    }

    // Cancela el pedido. No se puede cancelar uno ya entregado.
    public void Cancelar(string motivo)
    {
        if (Estado == EstadoPedido.Entregado)
            throw new InvalidOperationException("No se puede cancelar un pedido que ya se entregó.");
        if (Estado == EstadoPedido.Cancelado)
            return; // Cancelar dos veces no cambia nada.
        Estado = EstadoPedido.Cancelado;
        MotivoCancelacion = motivo ?? "";
    }

    // Marca el pedido como entregado. Solo un pedido pendiente se puede entregar.
    public void MarcarEntregado(DateTime fechaEntrega, int tiempoTraslado)
    {
        if (Estado != EstadoPedido.Pendiente)
            throw new InvalidOperationException("Solo se puede entregar un pedido pendiente. Estado actual: " + Estado + ".");
        if (tiempoTraslado < 0)
            throw new ArgumentException("El tiempo de traslado no puede ser negativo.", nameof(tiempoTraslado));

        Estado = EstadoPedido.Entregado;
        FechaEntrega = fechaEntrega;
        TiempoTraslado = tiempoTraslado;
    }

    // Monto entre minutos. Si el tiempo es 0 se toma como 1 minuto para no dividir entre cero.
    internal static decimal CalcularRendimiento(decimal monto, int tiempo)
    {
        int divisor = tiempo > 0 ? tiempo : 1;
        return monto / divisor;
    }
}