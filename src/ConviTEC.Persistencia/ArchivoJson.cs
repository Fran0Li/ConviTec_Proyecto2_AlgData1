using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConviTEC.Persistencia;

// Lee y escribe arreglos de datos en archivos JSON (documento de diseño, 5.10).
// Es la base de la persistencia de cada sede: pedidos, ventas, reservas, productos, inventario...
//
// Escritura segura: se escribe todo en <archivo>.tmp, se fuerza a disco y solo entonces reemplaza al
// archivo real. Si el programa se cae a medias, el archivo anterior queda intacto (RNF-15 y RNF-16);
// lo único que queda es un .tmp huérfano, que el .gitignore ignora y la siguiente escritura sobreescribe.
//
// Solo trabaja con arreglos nativos, porque el enunciado no permite colecciones del lenguaje (RNF-05).
public static class ArchivoJson
{
    // Las sedes atienden varias peticiones a la vez; este candado evita que dos escrituras se pisen
    // en el mismo .tmp o que una lectura vea un archivo a medio reemplazar.
    private static readonly object Candado = new object();

    private static readonly JsonSerializerOptions Opciones = CrearOpciones();

    private static JsonSerializerOptions CrearOpciones()
    {
        JsonSerializerOptions opciones = new JsonSerializerOptions
        {
            WriteIndented = true,                    // el archivo se puede abrir y leer a mano
            PropertyNameCaseInsensitive = true,      // "nombre" y "Nombre" valen igual al leer
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping   // tildes y ñ tal cual, no \u00E9
        };
        opciones.Converters.Add(new JsonStringEnumConverter());     // "Pendiente" en vez de 1
        return opciones;
    }

    // Lee un archivo que contiene un arreglo JSON.
    // - Si el archivo no existe o está vacío, devuelve un arreglo vacío (primera vez que arranca la sede).
    // - Si el contenido no es un arreglo JSON válido, o tiene una entrada null, lanza FormatException
    //   que dice cuál archivo falló. No se tapa el error: sobrescribir un archivo dañado perdería datos.
    // - Si no se puede leer el archivo (permisos, está bloqueado), lanza IOException con la ruta.
    public static T[] LeerArreglo<T>(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            throw new ArgumentException("Falta la ruta del archivo.", nameof(ruta));

        lock (Candado)
        {
            if (!File.Exists(ruta))
                return new T[0];

            string texto;
            try
            {
                texto = File.ReadAllText(ruta, Encoding.UTF8);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new IOException("No se pudo leer el archivo " + ruta + ": " + e.Message, e);
            }

            if (string.IsNullOrWhiteSpace(texto))
                return new T[0];

            T[]? datos;
            try
            {
                datos = JsonSerializer.Deserialize<T[]>(texto, Opciones);
            }
            catch (JsonException e)
            {
                throw new FormatException("El archivo " + ruta + " no es un arreglo JSON válido: " + e.Message, e);
            }

            if (datos == null)
                return new T[0];

            for (int i = 0; i < datos.Length; i++)
            {
                if (datos[i] == null)
                    throw new FormatException("El archivo " + ruta + " tiene una entrada vacía (null) en la posición " + i + ".");
            }
            return datos;
        }
    }

    // Guarda un arreglo completo, reemplazando lo que hubiera. Crea la carpeta si no existe.
    // Lanza IOException con la ruta si no se puede escribir.
    public static void GuardarArreglo<T>(string ruta, T[] datos)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            throw new ArgumentException("Falta la ruta del archivo.", nameof(ruta));
        if (datos == null)
            throw new ArgumentNullException(nameof(datos));

        string json = JsonSerializer.Serialize(datos, Opciones);
        string completa = Path.GetFullPath(ruta);
        string temporal = completa + ".tmp";

        lock (Candado)
        {
            try
            {
                string? carpeta = Path.GetDirectoryName(completa);
                if (carpeta != null)
                    Directory.CreateDirectory(carpeta);

                using (FileStream flujo = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter escritor = new StreamWriter(flujo, new UTF8Encoding(false)))
                {
                    escritor.Write(json);
                    escritor.Flush();
                    flujo.Flush(true);   // fuerza el contenido a disco antes de reemplazar
                }

                if (File.Exists(completa))
                    File.Replace(temporal, completa, null);
                else
                    File.Move(temporal, completa);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new IOException("No se pudo guardar el archivo " + ruta + ": " + e.Message, e);
            }
        }
    }
}