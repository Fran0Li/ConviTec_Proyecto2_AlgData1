namespace ConviTEC.Core.Contratos;

// Contenido de contadores.json: el último consecutivo usado, para que al reiniciar la sede los
// identificadores (LIB-0042, V-LIB-0007...) no se repitan (decisión D-09).
public class Contadores
{
    // Último número de pedido usado. 0 si todavía no hay ninguno.
    public int UltimoPedido { get; set; }

    // Último número de venta usado. 0 si todavía no hay ninguna.
    public int UltimaVenta { get; set; }
}