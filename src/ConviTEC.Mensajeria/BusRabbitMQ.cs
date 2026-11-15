using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace ConviTEC.Mensajeria;

// Implementación de IBusMensajes con RabbitMQ (documento de diseño, sección 5.7).
//
//   convitec.directo  (direct) ──clave "SJO"──► sede.SJO
//   convitec.difusion (fanout) ──────────────► sede.LIB, sede.SJO, sede.ALA, sede.LIM
//
// Cada sede tiene una sola cola durable, sede.<CÓDIGO>, enlazada a los dos exchanges.
// Usa la librería RabbitMQ.Client 7 (API asíncrona).
public class BusRabbitMQ : IBusMensajes
{
    public const string ExchangeDirecto = "convitec.directo";
    public const string ExchangeDifusion = "convitec.difusion";

    private readonly OpcionesRabbitMQ opciones;
    private readonly string nombreCola;

    // Dónde se escriben los avisos (mensajes descartados, errores). Por defecto, la consola.
    private readonly Action<string> registrar;

    private IConnection? conexion;
    private IChannel? canalRecepcion;   // por aquí llegan los mensajes de la cola
    private IChannel? canalEnvio;       // por aquí se publican

    // Solo un envío a la vez por el canal de envío (un canal no se debe usar desde dos hilos a la vez).
    private readonly SemaphoreSlim candadoEnvio = new SemaphoreSlim(1, 1);

    private string? etiquetaConsumidor;
    private Func<Sobre, Task>? alRecibir;

    public BusRabbitMQ(OpcionesRabbitMQ opciones, Action<string>? registrar = null)
    {
        if (opciones == null)
            throw new ArgumentNullException(nameof(opciones));
        opciones.Validar();

        this.opciones = opciones;
        nombreCola = NombreCola(opciones.CodigoSede);
        this.registrar = registrar ?? Console.WriteLine;
    }

    // Nombre de la cola de una sede, por ejemplo "sede.LIB".
    public static string NombreCola(string codigoSede)
    {
        return "sede." + codigoSede;
    }

    // Con la recuperación automática, si RabbitMQ se cae la conexión deja de estar abierta
    // (Conectado = false) hasta que la librería logra reconectarse sola.
    public bool Conectado => conexion != null && conexion.IsOpen;

    public async Task IniciarAsync(Func<Sobre, Task> alRecibir)
    {
        if (alRecibir == null)
            throw new ArgumentNullException(nameof(alRecibir));
        if (conexion != null)
            throw new InvalidOperationException("El bus ya está iniciado.");
        this.alRecibir = alRecibir;

        var fabrica = new ConnectionFactory
        {
            HostName = opciones.Host,
            Port = opciones.Puerto,
            UserName = opciones.Usuario,
            Password = opciones.Contrasena,
            // Si la conexión se cae, la librería reintenta sola y vuelve a declarar exchanges,
            // cola, enlaces y el consumidor. Así no hay que programar la reconexión a mano.
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            // Nombre que aparece en el panel web, en la pestaña Connections.
            ClientProvidedName = "convitec-" + opciones.CodigoSede
        };

        try
        {
            conexion = await fabrica.CreateConnectionAsync();
        }
        catch (BrokerUnreachableException e)
        {
            // La sede atrapa este error y sigue en modo local (diseño, Tabla 16).
            throw new InvalidOperationException(
                "No se pudo conectar a RabbitMQ en " + opciones.Host + ":" + opciones.Puerto +
                ". ¿Está levantado el contenedor (docker compose up -d)?", e);
        }

        canalRecepcion = await conexion.CreateChannelAsync();
        canalEnvio = await conexion.CreateChannelAsync();

        await DeclararTopologiaAsync(canalRecepcion);

        // prefetch = 1: RabbitMQ entrega el siguiente mensaje solo cuando se confirmó el anterior.
        // Así los mensajes se procesan de uno en uno y en orden, como promete IBusMensajes.
        await canalRecepcion.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumidor = new AsyncEventingBasicConsumer(canalRecepcion);
        consumidor.ReceivedAsync += AlLlegarMensajeAsync;

        // autoAck: false → nosotros decidimos cuándo un mensaje queda procesado (ack manual).
        etiquetaConsumidor = await canalRecepcion.BasicConsumeAsync(nombreCola, autoAck: false, consumer: consumidor);
    }

    // Declara los dos exchanges, la cola de esta sede y sus enlaces.
    // Declarar algo que ya existe con los mismos datos no hace nada, así que se puede llamar
    // cada vez que arranca la sede sin importar cuál sede arrancó primero.
    private async Task DeclararTopologiaAsync(IChannel canal)
    {
        // durable: sobreviven si se reinicia RabbitMQ.
        await canal.ExchangeDeclareAsync(ExchangeDirecto, ExchangeType.Direct, durable: true, autoDelete: false);
        await canal.ExchangeDeclareAsync(ExchangeDifusion, ExchangeType.Fanout, durable: true, autoDelete: false);

        // exclusive: false y autoDelete: false → la cola sigue existiendo (y guardando mensajes)
        // aunque la sede esté apagada. Al volver a encenderse recibe lo que le llegó mientras tanto.
        await canal.QueueDeclareAsync(nombreCola, durable: true, exclusive: false, autoDelete: false);

        // Directo: solo los mensajes con clave = código de esta sede.
        await canal.QueueBindAsync(nombreCola, ExchangeDirecto, routingKey: opciones.CodigoSede);
        // Difusión: un fanout ignora la clave, le manda copia a todas las colas enlazadas.
        await canal.QueueBindAsync(nombreCola, ExchangeDifusion, routingKey: "");
    }

    // Se ejecuta por cada mensaje que llega a la cola (de uno en uno, por el prefetch = 1).
    private async Task AlLlegarMensajeAsync(object remitente, BasicDeliverEventArgs entrega)
    {
        IChannel canal = canalRecepcion!;

        // 1. Leer el JSON. El cuerpo solo es válido dentro de este método: se copia a texto ya.
        Sobre sobre;
        try
        {
            string json = Encoding.UTF8.GetString(entrega.Body.Span);
            sobre = SerializadorSobre.Deserializar(json);
        }
        catch (FormatException e)
        {
            // Nunca se va a poder leer: si vuelve a la cola rebotaría para siempre y, con
            // prefetch = 1, taparía los mensajes de atrás. Se descarta (sin reencolar).
            registrar("[Bus " + opciones.CodigoSede + "] Mensaje descartado por mal formado: " + e.Message);
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false);
            return;
        }

        // 2. Entregarlo a la sede. Si termina bien → ack; si lanza excepción → vuelve a la cola.
        try
        {
            await alRecibir!(sobre);
            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false);
        }
        catch (Exception e)
        {
            registrar("[Bus " + opciones.CodigoSede + "] Error al procesar " + sobre.IdMensaje +
                      "; vuelve a la cola: " + e.Message);
            await canal.BasicNackAsync(entrega.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public async Task EnviarAsync(Sobre sobre)
    {
        if (sobre == null)
            throw new ArgumentNullException(nameof(sobre));
        if (canalEnvio == null || !Conectado)
            throw new InvalidOperationException("No hay conexión con RabbitMQ; no se puede enviar el mensaje.");

        // Difusión → fanout (la clave no importa). Directo → exchange directo con clave = sede destino.
        string exchange = sobre.EsDifusion ? ExchangeDifusion : ExchangeDirecto;
        string clave = sobre.EsDifusion ? "" : sobre.SedeDestino!;

        byte[] cuerpo = Encoding.UTF8.GetBytes(SerializadorSobre.Serializar(sobre));
        var propiedades = new BasicProperties
        {
            Persistent = true,                 // se guarda en disco: no se pierde si RabbitMQ se reinicia
            ContentType = "application/json",
            MessageId = sobre.IdMensaje
        };

        await candadoEnvio.WaitAsync();
        try
        {
            await canalEnvio.BasicPublishAsync(exchange, clave, mandatory: false, basicProperties: propiedades, body: cuerpo);
        }
        finally
        {
            candadoEnvio.Release();
        }
    }

    public async Task DetenerAsync()
    {
        // Se puede llamar aunque nunca se haya iniciado o ya se haya detenido.
        try
        {
            if (canalRecepcion != null && etiquetaConsumidor != null && canalRecepcion.IsOpen)
                await canalRecepcion.BasicCancelAsync(etiquetaConsumidor);
        }
        catch (Exception e)
        {
            registrar("[Bus " + opciones.CodigoSede + "] Aviso al cancelar el consumidor: " + e.Message);
        }

        await CerrarAsync(canalRecepcion);
        await CerrarAsync(canalEnvio);
        if (conexion != null)
        {
            try
            {
                if (conexion.IsOpen)
                    await conexion.CloseAsync();
            }
            catch (Exception e)
            {
                registrar("[Bus " + opciones.CodigoSede + "] Aviso al cerrar la conexión: " + e.Message);
            }
            await conexion.DisposeAsync();
        }

        canalRecepcion = null;
        canalEnvio = null;
        conexion = null;
        etiquetaConsumidor = null;
    }

    private async Task CerrarAsync(IChannel? canal)
    {
        if (canal == null)
            return;
        try
        {
            if (canal.IsOpen)
                await canal.CloseAsync();
        }
        catch (Exception e)
        {
            registrar("[Bus " + opciones.CodigoSede + "] Aviso al cerrar un canal: " + e.Message);
        }
        await canal.DisposeAsync();
    }
}
