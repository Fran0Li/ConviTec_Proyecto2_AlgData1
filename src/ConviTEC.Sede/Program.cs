using System;
using System.IO;
using ConviTEC.Core.Configuracion;
using ConviTEC.Mensajeria;
using ConviTEC.Sede.Arranque;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Punto de entrada de una sede (API REST + frontend en wwwroot).
// Cada sede es esta misma aplicación con otra configuración:
//   dotnet run --project src/ConviTEC.Sede -- --sede LIB
// lee appsettings.LIB.json (código, nombre, puerto, ubicación y carpeta de datos) y appsettings.json (RabbitMQ),
// y reconstruye sus estructuras desde los archivos de data/<CÓDIGO>/.
var builder = WebApplication.CreateBuilder(args);

string codigo;
ConfiguracionSede sede;
OpcionesRabbitMQ rabbit;
ServiciosSede servicios;

try
{
    codigo = ConfiguracionSede.ObtenerCodigo(args, Environment.GetEnvironmentVariable("CONVITEC_SEDE"));

    string archivoSede = "appsettings." + codigo + ".json";
    if (!File.Exists(Path.Combine(builder.Environment.ContentRootPath, archivoSede)))
        throw new InvalidOperationException(
            "No existe " + archivoSede + " para la sede " + codigo + ". Sedes disponibles: LIB, ALA, SJO, LIM.");

    // El archivo de la sede se agrega después de los proveedores por defecto, así que por sí solo les
    // ganaría. Se vuelven a agregar las variables de entorno y la línea de comandos al final para que
    // sigan mandando (por ejemplo RabbitMQ__Host=192.168.1.20 cuando RabbitMQ está en la otra computadora).
    builder.Configuration.AddJsonFile(archivoSede, optional: false, reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);

    sede = builder.Configuration.GetSection("Sede").Get<ConfiguracionSede>() ?? new ConfiguracionSede();
    sede.Validar();
    if (sede.Codigo != codigo)
        throw new InvalidOperationException(
            "El archivo " + archivoSede + " dice que la sede es " + sede.Codigo + " y se pidió " + codigo + ".");

    // De aquí en adelante CarpetaDatos es una ruta completa, lista para la persistencia.
    sede.CarpetaDatos = ConfiguracionSede.ResolverCarpeta(sede.CarpetaDatos, AppContext.BaseDirectory);
    Directory.CreateDirectory(sede.CarpetaDatos);

    rabbit = builder.Configuration.GetSection("RabbitMQ").Get<OpcionesRabbitMQ>() ?? new OpcionesRabbitMQ();
    rabbit.CodigoSede = sede.Codigo;
    rabbit.Validar();

    // Reconstruye las estructuras de la sede desde sus archivos. Si alguno está dañado, no se levanta.
    servicios = ArranqueSede.Iniciar(sede, Console.WriteLine);
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or FormatException)
{
    Console.Error.WriteLine("No se pudo iniciar la sede: " + ex.Message);
    return 1;
}

builder.WebHost.UseUrls("http://localhost:" + sede.Puerto);
builder.Services.AddSingleton(sede);
builder.Services.AddSingleton(rabbit);
builder.Services.AddSingleton(servicios);
builder.Services.AddSingleton(servicios.Destinos);
builder.Services.AddControllers();

var app = builder.Build();

// Un error que nadie anticipó no muestra detalles técnicos al dependiente: responde 500 con un mensaje en español.
// (El detalle queda en la consola de la sede.)
app.UseExceptionHandler(manejador => manejador.Run(async contexto =>
{
    contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
    contexto.Response.ContentType = "application/json; charset=utf-8";
    await contexto.Response.WriteAsync("{\"mensaje\":\"Ocurrió un error en la sede. Intente de nuevo.\"}");
}));

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/", () => "ConviTEC: sede " + sede.Codigo + " (" + sede.Nombre + ") en construcción");

Console.WriteLine("Sede " + sede.Codigo + " (" + sede.Nombre + "): puerto " + sede.Puerto + ", datos en " + sede.CarpetaDatos);
app.Run();
return 0;