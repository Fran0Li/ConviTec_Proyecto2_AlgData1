using System;

namespace ConviTEC.Core.Modelos;


public class ParadaRecorrido
{
   
    public string Destino { get; set; } = "";

  
    public string[] Camino { get; set; } = new string[0];


    public int TiempoTramo { get; set; }


    public int TiempoAcumulado { get; set; }


    public string[] IdsPedidos { get; set; } = new string[0];
}


public class Recorrido
{
    public string CodigoSede { get; set; } = "";


    public string UbicacionInicial { get; set; } = "";


    public DateTime FechaSalida { get; set; }


    public ParadaRecorrido[] Paradas { get; set; } = new ParadaRecorrido[0];

    public string[] IdsPedidosNoAlcanzables { get; set; } = new string[0];


    public int TiempoTotal
    {
        get
        {
            if (Paradas.Length == 0)
                return 0;
            return Paradas[Paradas.Length - 1].TiempoAcumulado;
        }
    }

    // Cantidad de pedidos entregados en todo el recorrido.
    public int CantidadPedidosEntregados
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Paradas.Length; i++)
                total += Paradas[i].IdsPedidos.Length;
            return total;
        }
    }
}