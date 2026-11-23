using System;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Core.Servicios;

// Salidas de entregas: todos los días a las 11:00 y a las 15:00
public static class HorarioSalidas
{
    public static readonly TimeSpan Manana = new TimeSpan(11, 0, 0);
    public static readonly TimeSpan Tarde = new TimeSpan(15, 0, 0);

    // Próxima salida a partir de "ahora". Si es justo la hora de salida, cuenta esa.
    public static DateTime ProximaSalida(DateTime ahora)
    {
        if (ahora.TimeOfDay <= Manana)
            return ahora.Date + Manana;
        if (ahora.TimeOfDay <= Tarde)
            return ahora.Date + Tarde;
        return ahora.Date.AddDays(1) + Manana;
    }

    // Pedidos pendientes creados antes o a la hora de la salida
    public static Pedido[] PedidosParaSalida(Pedido[] pedidos, DateTime salida)
    {
        if (pedidos == null)
            throw new ArgumentNullException(nameof(pedidos));

        int cantidad = 0;
        for (int i = 0; i < pedidos.Length; i++)
        {
            if (EntraEnSalida(pedidos[i], salida))
                cantidad++;
        }

        Pedido[] resultado = new Pedido[cantidad];
        int k = 0;
        for (int i = 0; i < pedidos.Length; i++)
        {
            if (EntraEnSalida(pedidos[i], salida))
            {
                resultado[k] = pedidos[i];
                k++;
            }
        }
        return resultado;
    }

    private static bool EntraEnSalida(Pedido pedido, DateTime salida)
    {
        return pedido != null && pedido.Estado == EstadoPedido.Pendiente && pedido.FechaCreacion <= salida;
    }
}