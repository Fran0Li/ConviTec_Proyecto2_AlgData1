using ConviTEC.Core.Grafo;
using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;
using ConviTEC.Persistencia;

namespace ConviTEC.Sede.Arranque;

// Lo que la sede arma al arrancar a partir de sus archivos (diseño 5.10).
// Los controladores no construyen nada: reciben estas piezas ya listas.
public class ServiciosSede
{
    public ServiciosSede(RepositorioSedeArchivos repositorio, GrafoDistribucion grafo, ServicioRutas rutas,
        ServicioDestinos destinos, Producto[] catalogo, RegistroReservasArchivo reservas,
        AlmacenContadores contadores, RegistroPedidosArchivo pedidos)
    {
        Repositorio = repositorio;
        Grafo = grafo;
        Rutas = rutas;
        Destinos = destinos;
        Catalogo = catalogo;
        Reservas = reservas;
        Contadores = contadores;
        Pedidos = pedidos;
    }

    public RepositorioSedeArchivos Repositorio { get; }
    public GrafoDistribucion Grafo { get; }
    public ServicioRutas Rutas { get; }
    public ServicioDestinos Destinos { get; }

    // El catálogo tal como está en productos.json. Cuando esté el AVL del rol A, se inserta ahí.
    public Producto[] Catalogo { get; }

    public RegistroReservasArchivo Reservas { get; }

    // Numeración de pedidos y ventas que no se repite al reiniciar.
    public AlmacenContadores Contadores { get; }

    // Todos los pedidos de la sede, guardados en pedidos.json.
    public RegistroPedidosArchivo Pedidos { get; }
}
