using ConviTEC.Core.Configuracion;
using Microsoft.AspNetCore.Mvc;

namespace ConviTEC.Sede.Controllers;

// GET /api/sede: datos de la sede actual (diseño 5.8).
[ApiController]
[Route("api/sede")]
public class SedeController : ControllerBase
{
    private readonly ConfiguracionSede sede;

    public SedeController(ConfiguracionSede sede)
    {
        this.sede = sede;
    }

    [HttpGet]
    public IActionResult Obtener()
    {
        return Ok(new { codigo = sede.Codigo, nombre = sede.Nombre, ubicacion = sede.Ubicacion });
    }
}