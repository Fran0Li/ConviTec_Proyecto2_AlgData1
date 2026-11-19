using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Servicios;

namespace ConviTEC.Tests.Servicios;

// Pruebas de la memoria circular de ids (rol C).
public class MemoriaIdsTests
{
    [Fact]
    public void Agregar_IdNuevoDevuelveTrueYRepetidoFalse()
    {
        var memoria = new MemoriaIds(5);

        Assert.True(memoria.Agregar("LIB-1"));
        Assert.False(memoria.Agregar("LIB-1"));
        Assert.True(memoria.Contiene("LIB-1"));
        Assert.Equal(1, memoria.Cantidad);
    }

    [Fact]
    public void AlLlenarse_OlvidaElMasViejo()
    {
        var memoria = new MemoriaIds(3);
        memoria.Agregar("a");
        memoria.Agregar("b");
        memoria.Agregar("c");

        memoria.Agregar("d");

        Assert.False(memoria.Contiene("a"));
        Assert.True(memoria.Contiene("b"));
        Assert.True(memoria.Contiene("c"));
        Assert.True(memoria.Contiene("d"));
        Assert.Equal(3, memoria.Cantidad);
    }

    [Fact]
    public void DaVariasVueltasSinPerderLosUltimos()
    {
        var memoria = new MemoriaIds(3);
        for (int i = 0; i < 10; i++)
            memoria.Agregar("id" + i);

        Assert.True(memoria.Contiene("id9"));
        Assert.True(memoria.Contiene("id8"));
        Assert.True(memoria.Contiene("id7"));
        Assert.False(memoria.Contiene("id6"));
    }

    [Fact]
    public void Constructor_CapacidadCero_LanzaExcepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoriaIds(0));
    }

    [Fact]
    public void Agregar_IdVacio_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => new MemoriaIds(3).Agregar(" "));
    }
}

// Pruebas del despachador (rol C, diseño 5.7). No necesitan RabbitMQ.
public class DespachadorTests
{
    // Despachador de la sede LIB que no escribe avisos en consola.
    private static Despachador Crear(int capacidad = Despachador.CapacidadPorDefecto)
    {
        return new Despachador("LIB", capacidad, _ => { });
    }

    // Registra un manejador que cuenta cuántas veces lo llamaron.
    private static int[] Contar(Despachador despachador, TipoMensaje tipo)
    {
        int[] contador = new int[1];
        despachador.Registrar(tipo, _ => { contador[0]++; return Task.CompletedTask; });
        return contador;
    }

    [Fact]
    public async Task EntregaCadaMensajeAlManejadorDeSuTipo()
    {
        Despachador d = Crear();
        int[] solicitudes = Contar(d, TipoMensaje.SolicitudReserva);
        int[] confirmaciones = Contar(d, TipoMensaje.ConfirmarReserva);

        await d.ProcesarAsync(Sobre.ParaSede("SJO", "LIB", new SolicitudReserva("SJO-1-LIB", "SJO-1", new LineaMensaje[0])));

        Assert.Equal(1, solicitudes[0]);
        Assert.Equal(0, confirmaciones[0]);
    }

    [Fact]
    public async Task ElManejadorRecibeElSobreCompleto()
    {
        Despachador d = Crear();
        Sobre? recibido = null;
        d.Registrar(TipoMensaje.LiberarReserva, s => { recibido = s; return Task.CompletedTask; });
        Sobre enviado = Sobre.ParaSede("SJO", "LIB", new LiberarReserva("SJO-1-LIB", "Cancelado"));

        await d.ProcesarAsync(enviado);

        Assert.Same(enviado, recibido);
    }

    [Fact]
    public async Task IgnoraSusPropiasDifusiones()
    {
        Despachador d = Crear();
        int[] actualizaciones = Contar(d, TipoMensaje.ActualizacionInventario);

        await d.ProcesarAsync(Sobre.ParaTodas("LIB", new ActualizacionInventario()));

        Assert.Equal(0, actualizaciones[0]);
    }

    [Fact]
    public async Task AceptaDifusionesDeOtrasSedes()
    {
        Despachador d = Crear();
        int[] actualizaciones = Contar(d, TipoMensaje.ActualizacionInventario);

        await d.ProcesarAsync(Sobre.ParaTodas("SJO", new ActualizacionInventario()));

        Assert.Equal(1, actualizaciones[0]);
    }

    [Fact]
    public async Task IgnoraDirectosParaOtraSede()
    {
        Despachador d = Crear();
        int[] confirmaciones = Contar(d, TipoMensaje.ConfirmarReserva);

        await d.ProcesarAsync(Sobre.ParaSede("SJO", "ALA", new ConfirmarReserva("SJO-1-ALA")));

        Assert.Equal(0, confirmaciones[0]);
    }

    [Fact]
    public async Task IgnoraMensajesRepetidos()
    {
        Despachador d = Crear();
        int[] confirmaciones = Contar(d, TipoMensaje.ConfirmarReserva);
        Sobre sobre = Sobre.ParaSede("SJO", "LIB", new ConfirmarReserva("SJO-1-LIB"));

        await d.ProcesarAsync(sobre);
        await d.ProcesarAsync(sobre);

        Assert.Equal(1, confirmaciones[0]);
    }

    [Fact]
    public async Task RepetidoQueLlegaReconstruido_TambienSeIgnora()
    {
        // Así llega un repetido de verdad: otro objeto Sobre con el mismo idMensaje.
        Despachador d = Crear();
        int[] confirmaciones = Contar(d, TipoMensaje.ConfirmarReserva);
        Sobre original = Sobre.ParaSede("SJO", "LIB", new ConfirmarReserva("SJO-1-LIB"));
        var copia = new Sobre(original.IdMensaje, "SJO", "LIB", original.FechaHora, new ConfirmarReserva("SJO-1-LIB"));

        await d.ProcesarAsync(original);
        await d.ProcesarAsync(copia);

        Assert.Equal(1, confirmaciones[0]);
    }

    [Fact]
    public async Task SiElManejadorFalla_LaExcepcionSubeYElReintentoSeProcesa()
    {
        Despachador d = Crear();
        int intentos = 0;
        d.Registrar(TipoMensaje.ConfirmarReserva, _ =>
        {
            intentos++;
            if (intentos == 1)
                throw new InvalidOperationException("Falla a propósito");
            return Task.CompletedTask;
        });
        Sobre sobre = Sobre.ParaSede("SJO", "LIB", new ConfirmarReserva("SJO-1-LIB"));

        // La excepción llega al bus, que devuelve el mensaje a la cola...
        await Assert.ThrowsAsync<InvalidOperationException>(() => d.ProcesarAsync(sobre));
        // ...y cuando RabbitMQ lo reenvía, NO se toma como repetido.
        await d.ProcesarAsync(sobre);

        Assert.Equal(2, intentos);
    }

    [Fact]
    public async Task TipoSinManejador_SeIgnoraSinError()
    {
        Despachador d = Crear();

        await d.ProcesarAsync(Sobre.ParaTodas("SJO", new SolicitudSincronizacion()));
    }

    [Fact]
    public async Task ConMemoriaChica_UnIdMuyViejoSeOlvida()
    {
        Despachador d = Crear(capacidad: 2);
        int[] confirmaciones = Contar(d, TipoMensaje.ConfirmarReserva);
        Sobre viejo = Sobre.ParaSede("SJO", "LIB", new ConfirmarReserva("1"));

        await d.ProcesarAsync(viejo);
        await d.ProcesarAsync(Sobre.ParaSede("SJO", "LIB", new ConfirmarReserva("2")));
        await d.ProcesarAsync(Sobre.ParaSede("SJO", "LIB", new ConfirmarReserva("3")));
        await d.ProcesarAsync(viejo);   // ya no está entre los 2 últimos

        Assert.Equal(4, confirmaciones[0]);
    }

    [Fact]
    public void Registrar_DosVecesElMismoTipo_LanzaExcepcion()
    {
        Despachador d = Crear();
        d.Registrar(TipoMensaje.ConfirmarReserva, _ => Task.CompletedTask);

        Assert.Throws<InvalidOperationException>(() => d.Registrar(TipoMensaje.ConfirmarReserva, _ => Task.CompletedTask));
    }

    [Fact]
    public void Registrar_ManejadorNulo_LanzaExcepcion()
    {
        Assert.Throws<ArgumentNullException>(() => Crear().Registrar(TipoMensaje.ConfirmarReserva, null!));
    }

    [Fact]
    public void Registrar_TipoInexistente_LanzaExcepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Crear().Registrar((TipoMensaje)99, _ => Task.CompletedTask));
    }

    [Fact]
    public void Constructor_SinCodigoDeSede_LanzaExcepcion()
    {
        Assert.Throws<ArgumentException>(() => new Despachador(""));
    }

    [Fact]
    public async Task ProcesarNulo_LanzaExcepcion()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Crear().ProcesarAsync(null!));
    }
}
