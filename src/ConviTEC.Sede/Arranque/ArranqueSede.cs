using System;
using ConviTEC.Core.Configuracion;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Grafo;
using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;
using ConviTEC.Persistencia;

namespace ConviTEC.Sede.Arranque;

// Reconstruye lo que la sede necesita desde sus archivos, en el orden del diseño 5.10.
// Si un archivo falta o está dañado lanza FileNotFoundException, FormatException o InvalidOperationException
// con un mensaje que dice cuál; Program.cs lo muestra y no levanta la sede (RNF-16 y PR-14).
public static class ArranqueSede
{
    public static ServiciosSede Iniciar(ConfiguracionSede sede, Action<string> registrar)
    {
        RepositorioSedeArchivos repositorio = new RepositorioSedeArchivos(sede.CarpetaDatos);

        // 1. Rutas: se carga la matriz de adyacencia y se comprueba que la sede esté en el grafo.
        GrafoDistribucion grafo = CargadorGrafo.DesdeRutas(repositorio.LeerRutas());
        if (!grafo.ExisteUbicacion(sede.Ubicacion))
            throw new InvalidOperationException("La ubicación \"" + sede.Ubicacion + "\" de la sede " + sede.Codigo + " no aparece en " + RepositorioSedeArchivos.ArchivoRutas + ".");
        registrar("Rutas: " + grafo.CantidadUbicaciones + " ubicaciones y " + grafo.CantidadRutas + " rutas.");

        // 2. Catálogo e inventario. Falta insertarlos en el AVL (rol A).
        Producto[] catalogo = repositorio.LeerProductos();
        LineaInventario[] inventario = repositorio.LeerInventario();
        for (int i = 0; i < inventario.Length; i++)
        {
            if (!ExisteProducto(catalogo, inventario[i].Codigo))
                registrar("Aviso: el inventario tiene el producto " + inventario[i].Codigo + ", que no está en el catálogo.");
        }
        registrar("Catálogo: " + catalogo.Length + " productos. Inventario: " + inventario.Length + " líneas.");

        // 3. Reservas. Falta liberar las vencidas (servicio de reservas, rol C).
        RegistroReservasArchivo reservas =
            new RegistroReservasArchivo(repositorio.RutaDe(RepositorioSedeArchivos.ArchivoReservas));
        registrar("Reservas guardadas: " + reservas.Cantidad + " (pendientes: " + reservas.Pendientes().Length + ").");

        // 4. Pedidos y ventas. El registro de pedidos deja los contadores al día. Falta cancelar los que
        // quedaron esperando reservas (servicio de pedidos, rol C).
        AlmacenContadores contadores = new AlmacenContadores(repositorio);
        RegistroPedidosArchivo pedidos = new RegistroPedidosArchivo(repositorio, contadores);
        Venta[] ventas = repositorio.LeerVentas();
        registrar("Pedidos: " + pedidos.Cantidad + " (esperando reservas: " + pedidos.EnEspera().Length +
            ", pendientes de entrega: " + pedidos.Pendientes().Length + "). Ventas: " + ventas.Length +
            ". Últimos consecutivos: pedido " + contadores.UltimoPedido + ", venta " + contadores.UltimaVenta + ".");

        // 5. Inventario global (la copia de las demás sedes). Falta conectar RabbitMQ y pedir sincronización.
        InventarioDeSede[] global = repositorio.LeerInventarioGlobal();
        registrar("Inventario global: copia de " + global.Length + " sedes.");

        return new ServiciosSede(repositorio, grafo, new ServicioRutas(grafo), new ServicioDestinos(grafo),
            catalogo, reservas, contadores, pedidos);
    }

    private static bool ExisteProducto(Producto[] catalogo, string codigo)
    {
        for (int i = 0; i < catalogo.Length; i++)
        {
            if (catalogo[i].Codigo == codigo)
                return true;
        }
        return false;
    }
}
