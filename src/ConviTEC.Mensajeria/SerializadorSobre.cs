using System;
using System.IO;
using System.Text;
using System.Text.Json;
using ConviTEC.Core.Mensajes;

namespace ConviTEC.Mensajeria;

// Convierte un Sobre a JSON y de vuelta (documento de diseño, sección 5.7).
// El JSON tiene la forma del diseño:
//   { "idMensaje", "tipo", "sedeOrigen", "sedeDestino", "fechaHora", "contenido": { ... } }
// Core no sabe nada de JSON: esta clase es la única que traduce entre el sobre y el texto
// que viaja por RabbitMQ.
public static class SerializadorSobre
{
    // Los nombres en JSON van en camelCase ("idReserva"), las propiedades en C# en PascalCase ("IdReserva").
    private static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ---------- Sobre → JSON ----------

    public static string Serializar(Sobre sobre)
    {
        if (sobre == null)
            throw new ArgumentNullException(nameof(sobre), "No se puede serializar un sobre nulo.");

        using var memoria = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(memoria))
        {
            escritor.WriteStartObject();
            escritor.WriteString("idMensaje", sobre.IdMensaje);
            escritor.WriteString("tipo", sobre.Tipo.ToString());   // el nombre, por ejemplo "SolicitudReserva"
            escritor.WriteString("sedeOrigen", sobre.SedeOrigen);
            if (sobre.SedeDestino == null)
                escritor.WriteNull("sedeDestino");                  // difusión
            else
                escritor.WriteString("sedeDestino", sobre.SedeDestino);
            escritor.WriteString("fechaHora", sobre.FechaHora);     // ISO 8601 con zona horaria

            // El contenido se serializa con su tipo real (SolicitudReserva, etc.), no con la
            // clase base: si no, solo saldrían las propiedades de ContenidoMensaje.
            JsonElement contenido = JsonSerializer.SerializeToElement(sobre.Contenido, sobre.Contenido.GetType(), Opciones);

            // Se copian sus campos menos "tipo": ese ya va en el sobre y el diseño no lo repite.
            escritor.WriteStartObject("contenido");
            foreach (JsonProperty campo in contenido.EnumerateObject())
            {
                if (campo.Name != "tipo")
                    campo.WriteTo(escritor);
            }
            escritor.WriteEndObject();

            escritor.WriteEndObject();
        }
        return Encoding.UTF8.GetString(memoria.ToArray());
    }

    // ---------- JSON → Sobre ----------

    // Lanza FormatException si el texto no es un sobre válido (JSON roto, falta un campo,
    // tipo desconocido, etc.). Así quien recibe solo tiene que atrapar un tipo de error.
    public static Sobre Deserializar(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("El mensaje recibido está vacío.");

        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
                throw new FormatException("El mensaje recibido no es un objeto JSON.");

            string idMensaje = LeerTexto(raiz, "idMensaje");
            TipoMensaje tipo = LeerTipo(raiz);
            string sedeOrigen = LeerTexto(raiz, "sedeOrigen");
            string? sedeDestino = LeerTextoOpcional(raiz, "sedeDestino");
            DateTimeOffset fechaHora = LeerFecha(raiz);

            if (!raiz.TryGetProperty("contenido", out JsonElement elementoContenido) ||
                elementoContenido.ValueKind != JsonValueKind.Object)
                throw new FormatException("El mensaje no tiene el campo \"contenido\".");

            // Opción A: el "tipo" del sobre dice qué clase hija hay que crear.
            ContenidoMensaje contenido = tipo switch
            {
                TipoMensaje.SolicitudReserva => LeerContenido<SolicitudReserva>(elementoContenido),
                TipoMensaje.RespuestaReserva => LeerContenido<RespuestaReserva>(elementoContenido),
                TipoMensaje.ConfirmarReserva => LeerContenido<ConfirmarReserva>(elementoContenido),
                TipoMensaje.LiberarReserva => LeerContenido<LiberarReserva>(elementoContenido),
                TipoMensaje.ActualizacionInventario => LeerContenido<ActualizacionInventario>(elementoContenido),
                TipoMensaje.SolicitudSincronizacion => LeerContenido<SolicitudSincronizacion>(elementoContenido),
                TipoMensaje.EstadoInventario => LeerContenido<EstadoInventario>(elementoContenido),
                _ => throw new FormatException("Tipo de mensaje sin traducción: " + tipo + ".")
            };

            // El constructor completo conserva el id y la fecha que puso el emisor.
            return new Sobre(idMensaje, sedeOrigen, sedeDestino, fechaHora, contenido);
        }
        catch (JsonException e)
        {
            throw new FormatException("El mensaje recibido no es JSON válido: " + e.Message, e);
        }
        catch (ArgumentException e)
        {
            // El constructor del Sobre rechazó algún dato (por ejemplo, id vacío).
            throw new FormatException("El mensaje recibido tiene datos inválidos: " + e.Message, e);
        }
    }

    // ---------- Ayudantes de lectura ----------

    private static string LeerTexto(JsonElement raiz, string nombre)
    {
        if (!raiz.TryGetProperty(nombre, out JsonElement valor) || valor.ValueKind != JsonValueKind.String)
            throw new FormatException("El mensaje no tiene el campo de texto \"" + nombre + "\".");
        return valor.GetString()!;
    }

    // Devuelve null si el campo no está o vale null (así viene sedeDestino en una difusión).
    private static string? LeerTextoOpcional(JsonElement raiz, string nombre)
    {
        if (!raiz.TryGetProperty(nombre, out JsonElement valor) || valor.ValueKind == JsonValueKind.Null)
            return null;
        if (valor.ValueKind != JsonValueKind.String)
            throw new FormatException("El campo \"" + nombre + "\" debe ser texto o null.");
        return valor.GetString();
    }

    private static TipoMensaje LeerTipo(JsonElement raiz)
    {
        string texto = LeerTexto(raiz, "tipo");
        // Solo se aceptan nombres ("SolicitudReserva"); Enum.TryParse también aceptaría "0" o "3".
        if (texto.Length == 0 || !char.IsLetter(texto[0]) ||
            !Enum.TryParse(texto, false, out TipoMensaje tipo) || !Enum.IsDefined(tipo))
            throw new FormatException("Tipo de mensaje desconocido: \"" + texto + "\".");
        return tipo;
    }

    private static DateTimeOffset LeerFecha(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("fechaHora", out JsonElement valor) ||
            valor.ValueKind != JsonValueKind.String ||
            !valor.TryGetDateTimeOffset(out DateTimeOffset fecha))
            throw new FormatException("El mensaje no tiene una \"fechaHora\" válida.");
        return fecha;
    }

    // Convierte el objeto "contenido" en la clase hija T usando su constructor vacío.
    private static T LeerContenido<T>(JsonElement contenido) where T : ContenidoMensaje
    {
        T? resultado = contenido.Deserialize<T>(Opciones);
        if (resultado == null)
            throw new FormatException("No se pudo leer el contenido del mensaje.");
        return resultado;
    }
}
