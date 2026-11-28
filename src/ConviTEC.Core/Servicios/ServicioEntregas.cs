using System;
using ConviTEC.Core.Algoritmos;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Grafo;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Servicios;

// Resultado de entregar un solo paso del recorrido
public class PasoEntrega
{
    // True si ya no queda ningún pedido alcanzable para esta salida
    public bool Terminado { get; set; }

    // Parada que se acaba de entregar. Null si Terminado.
    public ParadaRecorrido? Parada { get; set; }

    public Venta[] Ventas { get; set; } = new Venta[0];
}

// Planifica una salida de entregas y, al entregar, marca los pedidos y crea sus ventas.
// Las ventas se devuelven: quien llama (la sede) las agrega a ventas.json.
public class ServicioEntregas
{
    private readonly PlanificadorEntregas planificador;
    private readonly IRegistroPedidos registro;

    public ServicioEntregas(GrafoDistribucion grafo, IRegistroPedidos registro)
    {
        planificador = new PlanificadorEntregas(grafo);
        this.registro = registro ?? throw new ArgumentNullException(nameof(registro));
    }

    // Recorrido con los pendientes que entran en esa salida (11:00 o 15:00)
    public Recorrido PlanificarSalida(Sede sede, Pedido[] pendientes, DateTime salida)
    {
        return planificador.Planificar(sede, HorarioSalidas.PedidosParaSalida(pendientes, salida), salida);
    }

    // Paso a paso: calcula el siguiente punto desde la ubicación actual, entrega ahí y devuelve las ventas.
    // "minutosAcumulados" es el TiempoAcumulado de la parada anterior (0 al salir de la sede).
    public PasoEntrega EntregarSiguiente(Sede sede, string ubicacionActual, int minutosAcumulados,
        Pedido[] pendientes, DateTime salida)
    {
        if (sede == null)
            throw new ArgumentNullException(nameof(sede));
        if (minutosAcumulados < 0)
            throw new ArgumentException("Los minutos acumulados no pueden ser negativos.", nameof(minutosAcumulados));

        Pedido[] deLaSalida = HorarioSalidas.PedidosParaSalida(pendientes, salida);
        ParadaRecorrido? parada = planificador.SiguienteEntrega(ubicacionActual, deLaSalida);

        var paso = new PasoEntrega();
        if (parada == null)
        {
            paso.Terminado = true;
            return paso;
        }

        parada.TiempoAcumulado = minutosAcumulados + parada.TiempoTramo;

        var unaParada = new Recorrido();
        unaParada.CodigoSede = sede.Codigo;
        unaParada.UbicacionInicial = sede.Ubicacion;
        unaParada.FechaSalida = salida;
        unaParada.Paradas = new[] { parada };

        paso.Parada = parada;
        paso.Ventas = RegistrarEntregas(unaParada);
        return paso;
    }

    // Entrega cada pedido del recorrido y devuelve una venta por pedido entregado.
    // Si un pedido ya no está pendiente (por ejemplo, ya se entregó), se salta: no hay venta doble.
    public Venta[] RegistrarEntregas(Recorrido recorrido)
    {
        if (recorrido == null)
            throw new ArgumentNullException(nameof(recorrido));

        Venta[] ventas = new Venta[recorrido.CantidadPedidosEntregados];
        int cantidad = 0;

        for (int i = 0; i < recorrido.Paradas.Length; i++)
        {
            ParadaRecorrido parada = recorrido.Paradas[i];
            DateTime llegada = recorrido.FechaSalida.AddMinutes(parada.TiempoAcumulado);

            for (int j = 0; j < parada.IdsPedidos.Length; j++)
            {
                Pedido? pedido = registro.Buscar(parada.IdsPedidos[j]);
                if (pedido == null || pedido.Estado != EstadoPedido.Pendiente)
                    continue;

                pedido.MarcarEntregado(llegada, parada.TiempoAcumulado);
                registro.Guardar(pedido);

                ventas[cantidad] = Venta.DesdePedido(pedido);
                cantidad++;
            }
        }

        Venta[] resultado = new Venta[cantidad];
        for (int i = 0; i < cantidad; i++)
            resultado[i] = ventas[i];
        return resultado;
    }
}