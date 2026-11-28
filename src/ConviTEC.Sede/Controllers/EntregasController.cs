using System;
using ConviTEC.Core.Configuracion;
using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;
using ConviTEC.Sede.Arranque;
using Microsoft.AspNetCore.Mvc;
using SedeModelo = ConviTEC.Core.Modelos.Sede;

namespace ConviTEC.Sede.Controllers;

// Entregas de la sede (RF de recorridos, diseño 5.9). Toda la lógica está en ServicioEntregas.
//   GET  /api/entregas/plan?salida=...        recorrido planificado, sin entregar nada
//   POST /api/entregas?salida=...             entrega todo el recorrido y guarda las ventas
//   POST /api/entregas/siguiente?desde=...&acumulado=...&salida=...
//                                             entrega solo el siguiente punto (paso a paso)
// Si no se indica la salida, se usa la próxima (11:00 o 15:00).
[ApiController]
[Route("api/entregas")]
public class EntregasController : ControllerBase
{
    // ventas.json se lee y se reescribe completo: dos entregas a la vez no deben pisarse
    private static readonly object candadoVentas = new object();

    private readonly ConfiguracionSede sede;
    private readonly ServiciosSede servicios;
    private readonly ServicioEntregas entregas;

    public EntregasController(ConfiguracionSede sede, ServiciosSede servicios)
    {
        this.sede = sede;
        this.servicios = servicios;
        entregas = new ServicioEntregas(servicios.Grafo, servicios.Pedidos);
    }

    [HttpGet("plan")]
    public IActionResult Plan(DateTime? salida)
    {
        return Ok(entregas.PlanificarSalida(Sede(), servicios.Pedidos.Pendientes(), Salida(salida)));
    }

    [HttpPost]
    public IActionResult EntregarTodo(DateTime? salida)
    {
        Recorrido recorrido = entregas.PlanificarSalida(Sede(), servicios.Pedidos.Pendientes(), Salida(salida));
        Venta[] ventas = entregas.RegistrarEntregas(recorrido);
        GuardarVentas(ventas);
        return Ok(new { recorrido, ventas });
    }

    [HttpPost("siguiente")]
    public IActionResult EntregarSiguiente(string? desde, int acumulado, DateTime? salida)
    {
        string ubicacion = string.IsNullOrWhiteSpace(desde) ? sede.Ubicacion : desde;
        if (!servicios.Grafo.ExisteUbicacion(ubicacion))
            return BadRequest(new { mensaje = "La ubicación \"" + ubicacion + "\" no existe en la red de distribución." });
        if (acumulado < 0)
            return BadRequest(new { mensaje = "Los minutos acumulados no pueden ser negativos." });

        PasoEntrega paso = entregas.EntregarSiguiente(Sede(), ubicacion, acumulado,
            servicios.Pedidos.Pendientes(), Salida(salida));
        GuardarVentas(paso.Ventas);
        return Ok(paso);
    }

    private SedeModelo Sede()
    {
        return new SedeModelo(sede.Codigo, sede.Nombre, sede.Ubicacion);
    }

    private static DateTime Salida(DateTime? salida)
    {
        return salida ?? HorarioSalidas.ProximaSalida(DateTime.Now);
    }

    // Agrega las ventas nuevas al final de ventas.json
    private void GuardarVentas(Venta[] nuevas)
    {
        if (nuevas.Length == 0)
            return;

        lock (candadoVentas)
        {
            Venta[] anteriores = servicios.Repositorio.LeerVentas();
            Venta[] todas = new Venta[anteriores.Length + nuevas.Length];
            for (int i = 0; i < anteriores.Length; i++)
                todas[i] = anteriores[i];
            for (int i = 0; i < nuevas.Length; i++)
                todas[anteriores.Length + i] = nuevas[i];
            servicios.Repositorio.GuardarVentas(todas);
        }
    }
}