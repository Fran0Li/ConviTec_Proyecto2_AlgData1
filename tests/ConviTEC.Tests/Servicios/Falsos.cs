using ConviTEC.Core.Contratos;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;

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
