using System.Text;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Servicios;
using ConviTEC.Mensajeria;
using RabbitMQ.Client;

namespace ConviTEC.Tests.Mensajeria;

// Pruebas del bus que NO necesitan RabbitMQ.
public class BusRabbitMQSinServidorTests
{
    [Fact]
    public void Constructor_SinCodigoDeSede_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => new BusRabbitMQ(new OpcionesRabbitMQ()));
    }

    [Fact]
    public void AntesDeIniciar_NoEstaConectado()
    {
        var bus = new BusRabbitMQ(new OpcionesRabbitMQ { CodigoSede = "LIB" });

        Assert.False(bus.Conectado);
    }

    [Fact]
    public async Task Enviar_SinIniciar_LanzaExcepcion()
    {
        var bus = new BusRabbitMQ(new OpcionesRabbitMQ { CodigoSede = "LIB" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bus.EnviarAsync(Sobre.ParaSede("LIB", "SJO", new ConfirmarReserva("X"))));
    }

    [Fact]
    public async Task Iniciar_SinServidor_LanzaInvalidOperationException()
    {
        // Puerto 1: ahí no hay ningún RabbitMQ, la conexión se rechaza enseguida.
        var bus = new BusRabbitMQ(new OpcionesRabbitMQ { CodigoSede = "LIB", Puerto = 1 }, _ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.IniciarAsync(_ => Task.CompletedTask));
        Assert.False(bus.Conectado);
    }

    [Fact]
    public async Task Detener_SinIniciar_NoFalla()
    {
        var bus = new BusRabbitMQ(new OpcionesRabbitMQ { CodigoSede = "LIB" });

        await bus.DetenerAsync();
        await bus.DetenerAsync();
    }
}

// Pruebas con RabbitMQ de verdad: necesitan Docker levantado (docker compose up -d).
// Si RabbitMQ no está, [FactConRabbitMQ] las marca como omitidas en vez de fallar.
// Para no correrlas aunque RabbitMQ esté:  dotnet test --filter "Categoria!=RabbitMQ"
// Usan las sedes de prueba "PRA" y "PRB" para no mezclarse con las cuatro sedes reales.
[Trait("Categoria", "RabbitMQ")]
public class BusRabbitMQTests
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(10);

    // sinAvisos: no escribe en consola los errores que la prueba provoca a propósito.
    private static BusRabbitMQ CrearBus(string codigo, bool sinAvisos = false)
    {
        return new BusRabbitMQ(new OpcionesRabbitMQ { CodigoSede = codigo }, sinAvisos ? _ => { } : null);
    }

    // Espera a que termine la tarea o falla la prueba si pasan 10 s.
    private static async Task<T> EsperarAsync<T>(Task<T> tarea)
    {
        Task primera = await Task.WhenAny(tarea, Task.Delay(Espera));
        Assert.True(primera == tarea, "El mensaje no llegó a tiempo.");
        return await tarea;
    }

    // Arranca un bus que avisa cuando le llega el mensaje con ese id. Los mensajes viejos que
    // hayan quedado en la cola de pruebas anteriores se confirman y se ignoran.
    private static async Task<TaskCompletionSource<Sobre>> EscucharAsync(BusRabbitMQ bus, string idEsperado)
    {
        var llegada = new TaskCompletionSource<Sobre>(TaskCreationOptions.RunContinuationsAsynchronously);
        await bus.IniciarAsync(sobre =>
        {
            if (sobre.IdMensaje == idEsperado)
                llegada.TrySetResult(sobre);
            return Task.CompletedTask;
        });
        return llegada;
    }

    // Meta del domingo 15: una sede le manda un mensaje directo a otra.
    [FactConRabbitMQ]
    public async Task DosSedes_MensajeDirectoLlegaASuDestino()
    {
        BusRabbitMQ pra = CrearBus("PRA");
        BusRabbitMQ prb = CrearBus("PRB");
        try
        {
            var lineas = new[] { new LineaMensaje("P040", 6) };
            Sobre enviado = Sobre.ParaSede("PRA", "PRB", new SolicitudReserva("PRA-0001-PRB", "PRA-0001", lineas));

            TaskCompletionSource<Sobre> llegada = await EscucharAsync(prb, enviado.IdMensaje);
            await pra.IniciarAsync(_ => Task.CompletedTask);
            Assert.True(pra.Conectado);

            await pra.EnviarAsync(enviado);
            Sobre recibido = await EsperarAsync(llegada.Task);

            Assert.Equal("PRA", recibido.SedeOrigen);
            Assert.Equal("PRB", recibido.SedeDestino);
            var c = Assert.IsType<SolicitudReserva>(recibido.Contenido);
            Assert.Equal("PRA-0001-PRB", c.IdReserva);
            Assert.Equal(6, c.Lineas[0].Cantidad);
        }
        finally
        {
            await pra.DetenerAsync();
            await prb.DetenerAsync();
        }
    }

    // La difusión le llega a todas, incluida la que la envió (filtrarla es del despachador).
    [FactConRabbitMQ]
    public async Task Difusion_LlegaALasOtrasYALaQueLaEnvio()
    {
        BusRabbitMQ pra = CrearBus("PRA");
        BusRabbitMQ prb = CrearBus("PRB");
        try
        {
            Sobre enviado = Sobre.ParaTodas("PRA", new ActualizacionInventario(new[] { new LineaMensaje("P040", 14) }));

            TaskCompletionSource<Sobre> llegadaPrb = await EscucharAsync(prb, enviado.IdMensaje);
            TaskCompletionSource<Sobre> llegadaPra = await EscucharAsync(pra, enviado.IdMensaje);

            await pra.EnviarAsync(enviado);

            Sobre enPrb = await EsperarAsync(llegadaPrb.Task);
            Sobre enPra = await EsperarAsync(llegadaPra.Task);
            Assert.True(enPrb.EsDifusion);
            Assert.Equal("PRA", enPra.SedeOrigen);
        }
        finally
        {
            await pra.DetenerAsync();
            await prb.DetenerAsync();
        }
    }

    // Si alRecibir lanza una excepción, el mensaje vuelve a la cola y llega otra vez.
    [FactConRabbitMQ]
    public async Task SiFallaElProcesamiento_ElMensajeVuelveALlegar()
    {
        BusRabbitMQ pra = CrearBus("PRA");
        BusRabbitMQ prb = CrearBus("PRB", sinAvisos: true);
        try
        {
            Sobre enviado = Sobre.ParaSede("PRA", "PRB", new ConfirmarReserva("PRA-0002-PRB"));
            int intentos = 0;
            var segundoIntento = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            await prb.IniciarAsync(sobre =>
            {
                if (sobre.IdMensaje != enviado.IdMensaje)
                    return Task.CompletedTask;
                intentos++;
                if (intentos == 1)
                    throw new Exception("Falla a propósito en el primer intento");
                segundoIntento.TrySetResult(intentos);
                return Task.CompletedTask;
            });
            await pra.IniciarAsync(_ => Task.CompletedTask);

            await pra.EnviarAsync(enviado);

            Assert.Equal(2, await EsperarAsync(segundoIntento.Task));
        }
        finally
        {
            await pra.DetenerAsync();
            await prb.DetenerAsync();
        }
    }

    // Bus + despachador juntos: si RabbitMQ entrega dos veces el mismo mensaje, se procesa una sola vez.
    [FactConRabbitMQ]
    public async Task ConDespachador_ElMensajeRepetidoSeProcesaUnaVez()
    {
        BusRabbitMQ pra = CrearBus("PRA");
        BusRabbitMQ prb = CrearBus("PRB");
        try
        {
            Sobre repetido = Sobre.ParaSede("PRA", "PRB", new ConfirmarReserva("PRA-0004-PRB"));
            Sobre marca = Sobre.ParaSede("PRA", "PRB", new LiberarReserva("PRA-0005-PRB", "marca"));
            int confirmaciones = 0;
            var llegoLaMarca = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var despachador = new Despachador("PRB", registrar: _ => { });
            despachador.Registrar(TipoMensaje.ConfirmarReserva, s =>
            {
                if (s.IdMensaje == repetido.IdMensaje)
                    confirmaciones++;
                return Task.CompletedTask;
            });
            despachador.Registrar(TipoMensaje.LiberarReserva, s =>
            {
                if (s.IdMensaje == marca.IdMensaje)
                    llegoLaMarca.TrySetResult(true);
                return Task.CompletedTask;
            });

            await prb.IniciarAsync(despachador.ProcesarAsync);
            await pra.IniciarAsync(_ => Task.CompletedTask);

            // El mismo sobre dos veces y luego la marca. Los mensajes llegan en orden, así que
            // cuando llega la marca ya pasaron las dos copias.
            await pra.EnviarAsync(repetido);
            await pra.EnviarAsync(repetido);
            await pra.EnviarAsync(marca);

            await EsperarAsync(llegoLaMarca.Task);
            Assert.Equal(1, confirmaciones);
        }
        finally
        {
            await pra.DetenerAsync();
            await prb.DetenerAsync();
        }
    }

    // Un mensaje que no es un sobre válido se descarta y no tapa a los que vienen detrás.
    [FactConRabbitMQ]
    public async Task MensajeMalFormado_SeDescartaYNoBloqueaLaCola()
    {
        BusRabbitMQ prb = CrearBus("PRB", sinAvisos: true);
        try
        {
            Sobre valido = Sobre.ParaSede("PRA", "PRB", new ConfirmarReserva("PRA-0003-PRB"));
            var llegada = new TaskCompletionSource<Sobre>(TaskCreationOptions.RunContinuationsAsynchronously);
            await prb.IniciarAsync(sobre =>
            {
                if (sobre.IdMensaje == valido.IdMensaje)
                    llegada.TrySetResult(sobre);
                return Task.CompletedTask;
            });

            // Se publica basura directo con la librería, saltándose el bus.
            var fabrica = new ConnectionFactory { HostName = "localhost", UserName = "convitec", Password = "convitec2026" };
            await using (IConnection conexion = await fabrica.CreateConnectionAsync())
            await using (IChannel canal = await conexion.CreateChannelAsync())
            {
                await canal.BasicPublishAsync(BusRabbitMQ.ExchangeDirecto, "PRB", mandatory: false,
                    basicProperties: new BasicProperties(), body: Encoding.UTF8.GetBytes("esto no es un sobre"));
                await canal.BasicPublishAsync(BusRabbitMQ.ExchangeDirecto, "PRB", mandatory: false,
                    basicProperties: new BasicProperties(), body: Encoding.UTF8.GetBytes(SerializadorSobre.Serializar(valido)));
            }

            // Si la basura hubiera vuelto a la cola, quedaría adelante para siempre (prefetch = 1)
            // y el mensaje válido nunca llegaría.
            await EsperarAsync(llegada.Task);
        }
        finally
        {
            await prb.DetenerAsync();
        }
    }
}
