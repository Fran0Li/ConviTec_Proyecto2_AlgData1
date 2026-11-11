namespace ConviTEC.Core.Mensajes;

/// <summary>
/// Los 7 tipos de mensaje que se intercambian las sedes (documento de diseño, sección 5.7, Tabla 12).
/// </summary>
public enum TipoMensaje
{
    /// <summary>Directo a la sede dueña: pide reservar varias líneas (todo o nada).</summary>
    SolicitudReserva,

    /// <summary>Directo al solicitante: dice si la reserva se aceptó o no, y por qué.</summary>
    RespuestaReserva,

    /// <summary>Directo a la sede dueña: convierte la reserva en descuento real del inventario.</summary>
    ConfirmarReserva,

    /// <summary>Directo a la sede dueña: devuelve las unidades reservadas a disponible.</summary>
    LiberarReserva,

    /// <summary>Difusión a todas: cantidades disponibles nuevas de algunos productos de una sede.</summary>
    ActualizacionInventario,

    /// <summary>Difusión a todas: una sede que arranca pide el inventario de las demás.</summary>
    SolicitudSincronizacion,

    /// <summary>Directo al solicitante: inventario completo de una sede.</summary>
    EstadoInventario
}
