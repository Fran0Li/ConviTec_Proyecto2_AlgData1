using Microsoft.AspNetCore.Builder;

// Punto de entrada de una sede (API REST + frontend en wwwroot). Por ahora solo responde un texto.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "ConviTEC: sede en construcción");

app.Run();
