using System;
using System.Threading.Tasks;
using ConviTEC.Core.Mensajes;

namespace ConviTEC.Core.Contratos;

// Contrato del bus de mensajes entre sedes (documento de diseño, secciones 5.2 y 5.7).
// Core solo conoce esta interfaz: no sabe que detrás está RabbitMQ.
// La implementa ConviTEC.Mensajeria; en las pruebas se puede usar un bus falso en memoria.
public interface IBusMensajes
{
    // True mientras haya conexión con el intermediario (RabbitMQ).
    // Si es false, la sede solo acepta pedidos que se cubren con su inventario local (Tabla 16).
    bool Conectado { get; }

    // Se conecta al intermediario, prepara lo necesario para recibir los mensajes de esta
    // sede y empieza a recibirlos.
    // Por cada mensaje que llega se llama a alRecibir, de uno en uno y en el orden en que
    // llegaron. El mensaje se da por procesado solo cuando alRecibir termina sin error; si
    // lanza una excepción, el mensaje vuelve a la cola para intentarlo de nuevo.
    // Llegan también las difusiones que envió esta misma sede y, a veces, mensajes
    // repetidos: descartarlos le toca a quien recibe (el despachador), no al bus.
    Task IniciarAsync(Func<Sobre, Task> alRecibir);

    // Envía un mensaje. Si el sobre es una difusión (Sobre.EsDifusion) le llega a todas las
    // sedes; si no, solo a Sobre.SedeDestino.
    // Lanza una excepción si no hay conexión con el intermediario.
    Task EnviarAsync(Sobre sobre);

    // Deja de recibir mensajes y cierra la conexión. Se llama al apagar la sede.
    Task DetenerAsync();
}
