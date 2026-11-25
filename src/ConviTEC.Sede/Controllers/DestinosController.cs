using ConviTEC.Core.Configuracion;
using ConviTEC.Core.Servicios;
using Microsoft.AspNetCore.Mvc;

namespace ConviTEC.Sede.Controllers;

// GET /api/destinos: ubicaciones del grafo y si son alcanzables desde esta sede (RF-09).
// El controlador solo pide los datos a Core y los devuelve; no calcula nada.
[ApiController]
[Route("api/destinos")]
public class DestinosController : ControllerBase
{
    private readonly ConfiguracionSede sede;
    private readonly ServicioDestinos destinos;

    public DestinosController(ConfiguracionSede sede, ServicioDestinos destinos)
    {
        this.sede = sede;
        this.destinos = destinos;
    }

    [HttpGet]
    public IActionResult Listar()
    {
        return Ok(destinos.Listar(sede.Ubicacion));
    }
}