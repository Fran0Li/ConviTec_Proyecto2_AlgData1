using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Contratos;

// Copia del inventario de toda la cadena que guarda cada sede (diseño 5.4 y 5.7), más el catálogo.
// La implementa el rol A; la mantiene al día la sincronización (ActualizacionInventario y
// EstadoInventario). El registro de pedidos (rol C) la usa para decidir a qué sedes pedirles
// productos (D-02) y para sacar el nombre y el precio de cada producto.
//
// Es una COPIA: puede estar un poco atrasada. Por eso la sede dueña revisa su inventario real
// antes de aceptar una reserva; si la copia se equivocó, la reserva se rechaza y el pedido se cancela.
public interface IInventarioGlobal
{
    // Códigos de todas las sedes conocidas, incluida esta. Ejemplo: ["LIB", "ALA", "SJO", "LIM"].
    string[] Sedes();

    // Unidades disponibles de un producto en una sede, según la última información recibida.
    // Devuelve 0 si la sede o el producto no se conocen.
    int Disponible(string codigoSede, string codigoProducto);

    // El producto del catálogo con ese código, o null si no existe (D-10: el catálogo es el mismo
    // en todas las sedes).
    Producto? BuscarProducto(string codigoProducto);
}
