using ConviTEC.Core.Modelos;
using ConviTEC.Persistencia;
using Microsoft.AspNetCore.Mvc;

namespace ConviTEC.Sede.Controllers;

// Consulta de pedidos de la sede (RF-08, 5.8 del diseño). Solo lee; registrar un pedido (POST) es del
// servicio de pedidos y llega cuando estén las estructuras del rol A.
//   GET /api/pedidos              historial, el más reciente primero
//   GET /api/pedidos/pendientes   pendientes de entrega, en orden de llegada
//   GET /api/pedidos/{id}         un pedido, o 404 con mensaje
[ApiController]
[Route("api/pedidos")]
public class PedidosController : ControllerBase
{
    private readonly RegistroPedidosArchivo registro;

    public PedidosController(RegistroPedidosArchivo registro)
    {
        this.registro = registro;
    }

    [HttpGet]
    public IActionResult Historial()
    {
        Pedido[] todos = registro.Todos();   // en orden de registro
        Pedido[] masRecienteAntes = new Pedido[todos.Length];
        for (int i = 0; i < todos.Length; i++)
            masRecienteAntes[i] = todos[todos.Length - 1 - i];
        return Ok(masRecienteAntes);
    }

    [HttpGet("pendientes")]
    public IActionResult Pendientes()
    {
        return Ok(registro.Pendientes());
    }

    [HttpGet("{id}")]
    public IActionResult Obtener(string id)
    {
        Pedido? pedido = registro.Buscar(id);
        if (pedido == null)
            return NotFound(new { mensaje = "No existe el pedido " + id + " en esta sede." });
        return Ok(pedido);
    }
}
