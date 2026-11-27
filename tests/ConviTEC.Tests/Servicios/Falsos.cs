using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

// Bus de mentira: no manda nada, solo guarda lo que se le pidió enviar para revisarlo en la prueba.
public class BusFalso : IBusMensajes
{
    public List<Sobre> Enviados { get; } = new List<Sobre>();

    // Si es true, EnviarAsync lanza una excepción (para simular que RabbitMQ se cayó).
    public bool Fallar { get; set; }

    public bool Conectado => !Fallar;

    public Task IniciarAsync(Func<Sobre, Task> alRecibir) => Task.CompletedTask;

    public Task EnviarAsync(Sobre sobre)
    {
        if (Fallar)
            throw new InvalidOperationException("Bus caído (falso).");
        Enviados.Add(sobre);
        return Task.CompletedTask;
    }

    public Task DetenerAsync() => Task.CompletedTask;
}

// Registro de reservas de mentira: las guarda en memoria, sin archivo.
public class RegistroReservasFalso : IRegistroReservas
{
    private readonly List<Reserva> reservas = new List<Reserva>();

    public int VecesGuardado { get; private set; }

    // Si es true, Guardar lanza una excepción (para simular un error al escribir el archivo).
    public bool FallarAlGuardar { get; set; }

    public Reserva? Buscar(string idReserva) => reservas.Find(r => r.IdReserva == idReserva);

    public void Guardar(Reserva reserva)
    {
        if (FallarAlGuardar)
            throw new IOException("No se pudo escribir el archivo (falso).");
        VecesGuardado++;
        int i = reservas.FindIndex(r => r.IdReserva == reserva.IdReserva);
        if (i >= 0)
            reservas[i] = reserva;
        else
            reservas.Add(reserva);
    }

    public Reserva[] Pendientes() => reservas.FindAll(r => r.Estado == EstadoReserva.Pendiente).ToArray();
}

// Copia global de mentira: el catálogo y lo que "cree" esta sede que tienen las demás.
public class InventarioGlobalFalso : IInventarioGlobal
{
    private readonly string[] sedes;
    private readonly List<Producto> catalogo = new List<Producto>();
    private readonly Dictionary<string, int> disponibles = new Dictionary<string, int>();

    public InventarioGlobalFalso(params string[] sedes)
    {
        this.sedes = sedes;
    }

    public InventarioGlobalFalso Producto(string codigo, string nombre, decimal precio)
    {
        catalogo.Add(new Producto(codigo, nombre, "", "", precio));
        return this;
    }

    public InventarioGlobalFalso Fijar(string sede, string codigo, int cantidad)
    {
        disponibles[sede + "|" + codigo] = cantidad;
        return this;
    }

    public string[] Sedes() => sedes;

    public int Disponible(string codigoSede, string codigoProducto) =>
        disponibles.TryGetValue(codigoSede + "|" + codigoProducto, out int n) ? n : 0;

    public Producto? BuscarProducto(string codigoProducto) => catalogo.Find(p => p.Codigo == codigoProducto);

    public int VecesEscrita { get; private set; }

    public void ActualizarSede(string codigoSede, LineaMensaje[] lineas)
    {
        VecesEscrita++;
        foreach (LineaMensaje l in lineas)
            disponibles[codigoSede + "|" + l.Codigo] = l.Cantidad;
    }

    public void ReemplazarSede(string codigoSede, LineaMensaje[] lineas)
    {
        VecesEscrita++;
        foreach (string clave in disponibles.Keys.Where(k => k.StartsWith(codigoSede + "|")).ToList())
            disponibles.Remove(clave);
        ActualizarSede(codigoSede, lineas);
        VecesEscrita--;
    }
}

// Registro de pedidos de mentira: en memoria, con su contador.
public class RegistroPedidosFalso : IRegistroPedidos
{
    private readonly List<Pedido> pedidos = new List<Pedido>();
    private int ultimo;

    public int SiguienteNumero() => ++ultimo;

    public Pedido? Buscar(string idPedido) => pedidos.Find(p => p.Id == idPedido);

    public void Guardar(Pedido pedido)
    {
        int i = pedidos.FindIndex(p => p.Id == pedido.Id);
        if (i >= 0)
            pedidos[i] = pedido;
        else
            pedidos.Add(pedido);
    }

    public Pedido[] EnEspera() => pedidos.FindAll(p => p.Estado == EstadoPedido.Registrado).ToArray();
}

// Varias sedes conectadas en memoria, como si fuera RabbitMQ: cada mensaje le llega al despachador
// de su destino (o de todas, si es difusión), de uno en uno y en otro hilo, igual que con el bus real.
// Una sede "apagada" no recibe nada (sus mensajes se pierden), para probar el tiempo límite.
public class RedFalsa
{
    private readonly Dictionary<string, Despachador> despachadores = new Dictionary<string, Despachador>();
    private readonly Dictionary<string, SemaphoreSlim> turnos = new Dictionary<string, SemaphoreSlim>();
    private readonly HashSet<string> apagadas = new HashSet<string>();
    private int enCamino;

    public IBusMensajes Conectar(string codigoSede, Despachador despachador)
    {
        despachadores[codigoSede] = despachador;
        turnos[codigoSede] = new SemaphoreSlim(1, 1);
        return new BusDeSede(this);
    }

    public void Apagar(string codigoSede) => apagadas.Add(codigoSede);

    // Espera a que se terminen de procesar los mensajes que andan viajando.
    public async Task EsperarSilencioAsync()
    {
        for (int i = 0; i < 200 && Volatile.Read(ref enCamino) > 0; i++)
            await Task.Delay(10);
    }

    private void Entregar(Sobre sobre)
    {
        foreach (string sede in despachadores.Keys)
        {
            if (apagadas.Contains(sede))
                continue;
            if (!sobre.EsDifusion && sobre.SedeDestino != sede)
                continue;

            Despachador d = despachadores[sede];
            SemaphoreSlim turno = turnos[sede];
            Interlocked.Increment(ref enCamino);
            _ = Task.Run(async () =>
            {
                await turno.WaitAsync();
                try { await d.ProcesarAsync(sobre); }
                catch (Exception) { }
                finally
                {
                    turno.Release();
                    Interlocked.Decrement(ref enCamino);
                }
            });
        }
    }

    private class BusDeSede : IBusMensajes
    {
        private readonly RedFalsa red;
        public BusDeSede(RedFalsa red) { this.red = red; }
        public bool Conectado => true;
        public Task IniciarAsync(Func<Sobre, Task> alRecibir) => Task.CompletedTask;
        public Task EnviarAsync(Sobre sobre) { red.Entregar(sobre); return Task.CompletedTask; }
        public Task DetenerAsync() => Task.CompletedTask;
    }
}
