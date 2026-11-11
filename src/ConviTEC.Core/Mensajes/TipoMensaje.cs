namespace ConviTEC.Core.Mensajes;

// Los 7 tipos de mensaje que se intercambian las sedes (documento de diseño, sección 5.7, Tabla 12).
public enum TipoMensaje
{
    // Directo a la sede dueña: pide reservar varias líneas (todo o nada).
    SolicitudReserva,

    // Directo al solicitante: dice si la reserva se aceptó o no, y por qué.
    RespuestaReserva,

    // Directo a la sede dueña: convierte la reserva en descuento real del inventario.
    ConfirmarReserva,

    // Directo a la sede dueña: devuelve las unidades reservadas a disponible.
    LiberarReserva,

    // Difusión a todas: cantidades disponibles nuevas de algunos productos de una sede.
    ActualizacionInventario,

    // Difusión a todas: una sede que arranca pide el inventario de las demás.
    SolicitudSincronizacion,

    // Directo al solicitante: inventario completo de una sede.
    EstadoInventario
}
