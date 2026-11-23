using System.Net.Sockets;

namespace ConviTEC.Tests.Mensajeria;

// Igual que [Fact], pero si RabbitMQ no está levantado la prueba sale como OMITIDA (con el aviso
// de cómo levantarlo) en vez de fallar. Así `dotnet test` sin Docker no muestra nada en rojo, y
// con Docker (docker compose up -d) estas pruebas corren de verdad.
//
// xUnit lee el atributo al descubrir las pruebas: si en ese momento Skip tiene texto, no la corre.
public sealed class FactConRabbitMQAttribute : FactAttribute
{
    // Se revisa una sola vez por corrida, no una vez por prueba.
    private static readonly Lazy<bool> Disponible = new Lazy<bool>(HayRabbitMQ);

    public FactConRabbitMQAttribute()
    {
        if (!Disponible.Value)
            Skip = "RabbitMQ no está levantado en localhost:5672. Para correr esta prueba: docker compose up -d";
    }

    // Intenta abrir una conexión TCP al puerto de RabbitMQ, esperando como mucho medio segundo.
    private static bool HayRabbitMQ()
    {
        try
        {
            using var cliente = new TcpClient();
            return cliente.ConnectAsync("localhost", 5672).Wait(TimeSpan.FromMilliseconds(500)) && cliente.Connected;
        }
        catch (Exception)
        {
            // Conexión rechazada (no hay nada en ese puerto) u otro error de red.
            return false;
        }
    }
}
