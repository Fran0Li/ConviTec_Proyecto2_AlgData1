using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Contratos;

// Dónde guarda una sede las reservas que les hizo a otras sedes (diseño 5.4 y 5.10).
// Lo usa el servicio de reservas (rol C). La implementación real va con la tabla hash del rol A
// (buscar por idReserva) y la escritura en archivo del rol D (sobrevive a un reinicio).
public interface IRegistroReservas
{
    // La reserva con ese id, o null si no existe.
    Reserva? Buscar(string idReserva);

    // Agrega la reserva o, si ya hay una con el mismo IdReserva, la reemplaza.
    // Al volver, el cambio ya debe estar guardado en archivo.
    void Guardar(Reserva reserva);

    // Las reservas que siguen en estado Pendiente. Se usa para liberar las vencidas.
    // Si no hay ninguna, devuelve un arreglo vacío.
    Reserva[] Pendientes();
}
