using ConviTEC.Core.Grafo;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Contratos;

// Todo lo que Core necesita leer y guardar de los archivos de una sede (diseño 5.2 y 5.10).
// Core no sabe que detrás hay archivos JSON: lo implementa ConviTEC.Persistencia, y en las pruebas
// se puede usar uno falso en memoria. Cada sede tiene su propio repositorio sobre su carpeta data/<CÓDIGO>/.
//
// Reglas comunes:
// - Los Leer... devuelven arreglos (nunca null). Si el archivo no existe todavía devuelven un arreglo vacío,
//   salvo rutas y productos, que son obligatorios y lanzan FileNotFoundException si faltan.
// - Si un archivo está dañado o tiene datos imposibles, lanzan FormatException que dice cuál archivo y
//   cuál entrada. No se corrige en silencio: sobrescribir un archivo dañado perdería datos.
// - Los Guardar... reemplazan el archivo completo con escritura segura y lanzan IOException si no se pudo.
// - Las reservas de otras sedes las guarda el registro de reservas (IRegistroReservas), no este repositorio.
public interface IRepositorioSede
{
    // rutas.json: copia de las rutas de la sede (solo lectura). Con ellas se arma el grafo con
    // CargadorGrafo.DesdeRutas, que revisa tiempos y ubicaciones.
    Ruta[] LeerRutas();

    // productos.json: el catálogo (solo lectura). Un producto sin existencias sigue en el catálogo (D-10).
    Producto[] LeerProductos();

    // inventario.json: existencias y reservas por producto. Si todavía no existe (primer arranque),
    // se lee la copia inicial inventario.inicial.json.
    LineaInventario[] LeerInventario();
    void GuardarInventario(LineaInventario[] lineas);

    // pedidos.json: todos los pedidos de la sede, en cualquier estado.
    Pedido[] LeerPedidos();
    void GuardarPedidos(Pedido[] pedidos);

    // ventas.json: pedidos entregados, con sus líneas.
    Venta[] LeerVentas();
    void GuardarVentas(Venta[] ventas);

    // inventario_global.json: última copia conocida del inventario de las demás sedes.
    InventarioDeSede[] LeerInventarioGlobal();
    void GuardarInventarioGlobal(InventarioDeSede[] copia);

    // contadores.json: últimos consecutivos. Si no existe, devuelve todo en 0.
    Contadores LeerContadores();
    void GuardarContadores(Contadores contadores);
}