using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Contratos;

// Dónde guarda una sede sus pedidos (diseño 5.4 y 5.10). Lo usa el registro de pedidos (rol C).
// La implementación real va con las listas del rol A y la escritura en archivo del rol D.
public interface IRegistroPedidos
{
    // Número para el próximo pedido de esta sede: 1, 2, 3... Cada llamada devuelve uno nuevo, y el
    // contador se guarda en archivo para no repetir números después de un reinicio.
    // Con él se arma el id "LIB-0042" (D-09).
    int SiguienteNumero();

    // El pedido con ese id, o null si no existe.
    Pedido? Buscar(string idPedido);

    // Agrega el pedido o, si ya hay uno con el mismo Id, lo reemplaza.
    // Al volver, el cambio ya debe estar guardado en archivo.
    void Guardar(Pedido pedido);

    // Los pedidos que siguen en estado Registrado (esperando respuestas de reserva).
    // Al arrancar, la sede cancela estos pedidos (diseño 5.10, paso 4). Si no hay, arreglo vacío.
    Pedido[] EnEspera();
}
