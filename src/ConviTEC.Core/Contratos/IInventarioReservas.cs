using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Contratos;

// Lo que el servicio de reservas (rol C) necesita del inventario local de la sede (rol A).
// Diseño: 5.6 (flujo de un pedido), 5.7 (SolicitudReserva, ConfirmarReserva, LiberarReserva) y D-03.
//
// Cada producto del inventario tiene una cantidad y unas unidades reservadas:
//   disponible = cantidad - reservado
// Las reservas en dos fases mueven unidades entre esos dos números:
//   Reservar  → reservado += n             (fase 1: apartar)
//   Confirmar → cantidad  -= n, reservado -= n   (fase 2a: se vendió)
//   Liberar   → reservado -= n             (fase 2b: se devuelve a disponible)
//
// Cada método es atómico: el inventario usa su propio candado, así que un pedido local y una
// solicitud de otra sede no pueden apartar las mismas unidades a la vez.
//
// El inventario no conoce los idReserva: saber si una reserva ya se confirmó o liberó es
// tarea del servicio de reservas (idempotencia por idReserva). El inventario solo mueve números.
//
// Si el mismo producto aparece en dos líneas, sus cantidades se suman.
public interface IInventarioReservas
{
    // Aparta todas las líneas o ninguna (todo o nada).
    // Si un producto no existe o no tiene suficientes disponibles, no aparta nada, devuelve false
    // y explica en motivo qué faltó, por ejemplo "No hay suficientes unidades de P040 (pide 6, hay 4)".
    // Ese texto viaja en la RespuestaReserva para mostrárselo al dependiente.
    // Si devuelve true, motivo queda vacío.
    bool Reservar(LineaReserva[] lineas, out string motivo);

    // Fase 2a: lo apartado se vende. Por cada línea: cantidad -= n y reservado -= n.
    // Lanza InvalidOperationException si algún producto no existe o no tiene esas unidades
    // reservadas (sería un error de lógica, no algo normal).
    void Confirmar(LineaReserva[] lineas);

    // Fase 2b: lo apartado vuelve a estar disponible. Por cada línea: reservado -= n.
    // Mismas excepciones que Confirmar.
    void Liberar(LineaReserva[] lineas);

    // Unidades disponibles (cantidad - reservado) de un producto.
    // Se usa después de confirmar, para difundir la cantidad final en ActualizacionInventario.
    // Lanza ArgumentException si el producto no existe.
    int Disponible(string codigoProducto);
}
