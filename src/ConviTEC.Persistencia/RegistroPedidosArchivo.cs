using System;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Persistencia;

// Registro de los pedidos de la sede (implementa IRegistroPedidos, diseño 5.4 y 5.10).
//
// Los pedidos viven en un arreglo en memoria, en orden de llegada, y se guardan completos en pedidos.json
// cada vez que uno cambia. Al crear el registro se lee ese archivo, así que lo guardado sobrevive a cerrar y
// volver a abrir la sede. Los números de pedido salen del AlmacenContadores.
//
// Por ahora buscar es recorrer el arreglo. Cuando estén las listas del rol A se cambia el almacenamiento de
// adentro y el resto del sistema no se entera, porque solo conoce IRegistroPedidos.
public class RegistroPedidosArchivo : IRegistroPedidos
{
    private const int CapacidadInicial = 16;

    // La API y los mensajes de RabbitMQ llegan en hilos distintos.
    private readonly object candado = new object();
    private readonly IRepositorioSede repositorio;
    private readonly AlmacenContadores contadores;
    private Pedido[] pedidos = new Pedido[CapacidadInicial];
    private int cantidad;

    // Lee pedidos.json. Lanza FormatException si está dañado. Si el contador de pedidos quedó por debajo
    // del número más alto que ya aparece en los pedidos guardados, lo sube para no repetir ids.
    public RegistroPedidosArchivo(IRepositorioSede repositorio, AlmacenContadores contadores)
    {
        this.repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        this.contadores = contadores ?? throw new ArgumentNullException(nameof(contadores));

        Pedido[] guardados = repositorio.LeerPedidos();
        int mayor = 0;
        for (int i = 0; i < guardados.Length; i++)
        {
            Poner(guardados[i]);
            int numero = NumeroDe(guardados[i].Id);
            if (numero > mayor)
                mayor = numero;
        }
        contadores.AsegurarPedidoAlMenos(mayor);
    }

    // Cuántos pedidos hay registrados, en cualquier estado.
    public int Cantidad
    {
        get
        {
            lock (candado)
            {
                return cantidad;
            }
        }
    }

    // El número para el próximo pedido (1, 2, 3...), ya guardado en archivo.
    public int SiguienteNumero()
    {
        return contadores.SiguientePedido();
    }

    // El pedido con ese id, o null si no existe (o si el id viene vacío).
    public Pedido? Buscar(string idPedido)
    {
        if (string.IsNullOrWhiteSpace(idPedido))
            return null;

        lock (candado)
        {
            int indice = IndiceDe(idPedido);
            return indice == -1 ? null : pedidos[indice];
        }
    }

    // Agrega el pedido o reemplaza el que tenga el mismo Id, y guarda el archivo antes de volver.
    // Si no se puede escribir el archivo lanza IOException y deja el registro como estaba. (Si quien llama ya
    // modificó ese mismo objeto Pedido, esa modificación no se puede deshacer desde aquí.)
    public void Guardar(Pedido pedido)
    {
        if (pedido == null)
            throw new ArgumentNullException(nameof(pedido));
        if (string.IsNullOrWhiteSpace(pedido.Id))
            throw new ArgumentException("El pedido debe tener un Id.", nameof(pedido));

        lock (candado)
        {
            int indice = IndiceDe(pedido.Id);
            Pedido? anterior = indice == -1 ? null : pedidos[indice];

            Poner(pedido);
            try
            {
                repositorio.GuardarPedidos(Copia());
            }
            catch
            {
                if (anterior != null)
                {
                    pedidos[indice] = anterior;
                }
                else
                {
                    cantidad--;
                    pedidos[cantidad] = null!;
                }
                throw;
            }
        }
    }

    // Los pedidos que siguen Registrados, esperando respuestas de reserva. Arreglo vacío si no hay.
    // Al arrancar, la sede cancela estos pedidos (diseño 5.10, paso 4).
    public Pedido[] EnEspera()
    {
        return Filtrar(EstadoPedido.Registrado);
    }

    // Los pedidos Pendientes de entrega, en orden de llegada (GET /api/pedidos/pendientes).
    public Pedido[] Pendientes()
    {
        return Filtrar(EstadoPedido.Pendiente);
    }

    // Todos los pedidos en orden de llegada, en cualquier estado (GET /api/pedidos).
    public Pedido[] Todos()
    {
        lock (candado)
        {
            return Copia();
        }
    }

    private Pedido[] Filtrar(EstadoPedido estado)
    {
        lock (candado)
        {
            int total = 0;
            for (int i = 0; i < cantidad; i++)
            {
                if (pedidos[i].Estado == estado)
                    total++;
            }

            Pedido[] resultado = new Pedido[total];
            int k = 0;
            for (int i = 0; i < cantidad; i++)
            {
                if (pedidos[i].Estado == estado)
                {
                    resultado[k] = pedidos[i];
                    k++;
                }
            }
            return resultado;
        }
    }

    // Reemplaza el pedido con ese id o lo agrega al final, agrandando el arreglo si se llenó.
    private void Poner(Pedido pedido)
    {
        int indice = IndiceDe(pedido.Id);
        if (indice != -1)
        {
            pedidos[indice] = pedido;
            return;
        }

        if (cantidad == pedidos.Length)
        {
            Pedido[] mayor = new Pedido[pedidos.Length * 2];
            for (int i = 0; i < cantidad; i++)
                mayor[i] = pedidos[i];
            pedidos = mayor;
        }
        pedidos[cantidad] = pedido;
        cantidad++;
    }

    private int IndiceDe(string idPedido)
    {
        for (int i = 0; i < cantidad; i++)
        {
            if (pedidos[i].Id == idPedido)
                return i;
        }
        return -1;
    }

    // Arreglo del tamaño exacto, sin las casillas vacías del final.
    private Pedido[] Copia()
    {
        Pedido[] copia = new Pedido[cantidad];
        for (int i = 0; i < cantidad; i++)
            copia[i] = pedidos[i];
        return copia;
    }

    // El número de un id como "LIB-0042" (42). 0 si el id no tiene esa forma.
    private static int NumeroDe(string id)
    {
        int guion = id.LastIndexOf('-');
        if (guion < 0 || guion == id.Length - 1)
            return 0;

        int numero;
        if (int.TryParse(id.Substring(guion + 1), out numero) && numero > 0)
            return numero;
        return 0;
    }
}