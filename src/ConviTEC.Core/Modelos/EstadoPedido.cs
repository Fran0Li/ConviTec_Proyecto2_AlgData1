namespace ConviTEC.Core.Modelos;

// Estados por los que pasa un pedido.
//
//   Registrado ──► Pendiente ──► Entregado
//       │
//       └────────► Cancelado
public enum EstadoPedido
{
    // Recién creado: se están pidiendo las reservas a las sedes que suministran productos.
    Registrado,

    // Reservas confirmadas e inventarios descontados. Espera la próxima salida de entregas.
    Pendiente,

    // Ya se entregó en su destino. En este momento se registra la venta.
    Entregado,

    // No se pudo completar (sin existencias, sin ruta o tiempo límite). Las reservas se liberan.
    Cancelado
}