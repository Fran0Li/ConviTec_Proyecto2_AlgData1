using System;

namespace ConviTEC.Mensajeria;

// Datos para conectarse a RabbitMQ. Program.cs de cada sede los llena desde appsettings.json
// (sección "RabbitMQ"); así esta biblioteca no depende de la configuración de ASP.NET.
// Los valores por defecto calzan con el docker-compose.yml del repo (RabbitMQ en la misma compu).
public class OpcionesRabbitMQ
{
    // Compu donde corre RabbitMQ. "localhost" si está en la misma; la IP de la otra compu si no.
    public string Host { get; set; } = "localhost";

    public int Puerto { get; set; } = 5672;

    public string Usuario { get; set; } = "convitec";

    public string Contrasena { get; set; } = "convitec2026";

    // Código de la sede dueña de este bus, por ejemplo "LIB". Define su cola (sede.LIB)
    // y la clave con la que le llegan los mensajes directos. Es obligatorio.
    public string CodigoSede { get; set; } = "";

    // Revisa que no falte nada antes de conectarse.
    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("Falta el host de RabbitMQ.");
        if (Puerto <= 0 || Puerto > 65535)
            throw new ArgumentException("El puerto de RabbitMQ no es válido: " + Puerto + ".");
        if (string.IsNullOrWhiteSpace(Usuario))
            throw new ArgumentException("Falta el usuario de RabbitMQ.");
        if (string.IsNullOrWhiteSpace(CodigoSede))
            throw new ArgumentException("Falta el código de la sede.");
    }
}
