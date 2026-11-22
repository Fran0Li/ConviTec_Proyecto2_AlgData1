using System;
using System.IO;

namespace ConviTEC.Core.Configuracion;

// Configuración de una sede: quién es, en qué puerto atiende y dónde guarda sus archivos
// (documento de diseño, 5.10). Cada sede es la misma aplicación; lo único que cambia es esta
// configuración, que sale de appsettings.<CÓDIGO>.json (por ejemplo appsettings.LIB.json).
//
// Para agregar o cambiar una sede (el enunciado dice que pueden variar en la defensa) basta con
// crear su appsettings.<CÓDIGO>.json y su carpeta data/<CÓDIGO>/; no hay que tocar el código.
public class ConfiguracionSede
{
    // Nombre del archivo que marca la raíz del repositorio. Sirve para ubicar la carpeta data/.
    public const string ArchivoRaiz = "ConviTEC.sln";

    private const string Uso =
        "Indique la sede con --sede <CÓDIGO> (por ejemplo: dotnet run --project src/ConviTEC.Sede -- --sede LIB) " +
        "o con la variable de entorno CONVITEC_SEDE.";

    // Código único de la sede, en mayúsculas: LIB, ALA, SJO, LIM. Define su cola en RabbitMQ y su carpeta de datos.
    public string Codigo { get; set; } = "";

    // Nombre para mostrar en pantalla: Liberia, Alajuela...
    public string Nombre { get; set; } = "";

    // Puerto del servidor web de la sede. Debe ser distinto en cada sede si corren en la misma computadora.
    public int Puerto { get; set; }

    // Carpeta donde la sede guarda sus archivos. Si es relativa, se cuenta desde la raíz del repositorio.
    public string CarpetaDatos { get; set; } = "";

    // Averigua el código de la sede que se quiere levantar.
    // Orden de prioridad: argumento --sede LIB (o --sede=LIB) y, si no hay, la variable de entorno.
    // Devuelve el código en mayúsculas. Lanza ArgumentException si falta o tiene caracteres que no son A-Z ni 0-9
    // (el código se usa para armar nombres de archivo y de cola, así que no se aceptan rutas ni espacios).
    public static string ObtenerCodigo(string[] args, string? variableEntorno)
    {
        string? valor = null;

        for (int i = 0; i < args.Length && valor == null; i++)
        {
            if (args[i] == "--sede")
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException("Falta el código de la sede después de --sede. " + Uso);
                valor = args[i + 1];
            }
            else if (args[i].StartsWith("--sede=", StringComparison.Ordinal))
            {
                valor = args[i].Substring("--sede=".Length);
            }
        }

        if (valor == null)
            valor = variableEntorno;

        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("No se indicó la sede. " + Uso);

        string codigo = valor.Trim().ToUpperInvariant();
        if (!EsCodigoValido(codigo))
            throw new ArgumentException(
                "El código de sede \"" + valor + "\" no es válido: solo puede tener letras de la A a la Z y números.");

        return codigo;
    }

    // Revisa que no falte nada ni haya valores imposibles antes de arrancar.
    // Lanza ArgumentException con un mensaje que dice qué dato corregir.
    public void Validar()
    {
        if (!EsCodigoValido(Codigo))
            throw new ArgumentException("La configuración de la sede no tiene un código válido (letras A-Z y números).");
        if (string.IsNullOrWhiteSpace(Nombre))
            throw new ArgumentException("La sede " + Codigo + " no tiene nombre.");
        if (Puerto < 1024 || Puerto > 65535)
            throw new ArgumentException(
                "El puerto de la sede " + Codigo + " no es válido: " + Puerto + " (debe estar entre 1024 y 65535).");
        if (string.IsNullOrWhiteSpace(CarpetaDatos))
            throw new ArgumentException("La sede " + Codigo + " no tiene carpeta de datos.");
    }

    // Convierte la carpeta de datos en una ruta completa.
    // Si ya es absoluta se deja como está. Si es relativa (data/LIB) se busca hacia arriba, desde
    // puntoPartida, la carpeta que contiene ConviTEC.sln y se cuenta desde ahí. Así funciona igual si la
    // sede se arranca desde Visual Studio, desde la terminal o desde otra computadora con el repo clonado.
    // Lanza DirectoryNotFoundException si no encuentra la raíz del repositorio.
    public static string ResolverCarpeta(string carpetaDatos, string puntoPartida)
    {
        if (Path.IsPathRooted(carpetaDatos))
            return Path.GetFullPath(carpetaDatos);

        DirectoryInfo? carpeta = new DirectoryInfo(puntoPartida);
        while (carpeta != null)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, ArchivoRaiz)))
                return Path.GetFullPath(Path.Combine(carpeta.FullName, carpetaDatos));
            carpeta = carpeta.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encontró la raíz del repositorio (" + ArchivoRaiz + ") a partir de " + puntoPartida +
            ". Use una ruta absoluta en CarpetaDatos.");
    }

    // Solo A-Z y 0-9, y al menos un carácter. Se recorre a mano para no depender de otras clases.
    private static bool EsCodigoValido(string codigo)
    {
        if (string.IsNullOrEmpty(codigo))
            return false;

        for (int i = 0; i < codigo.Length; i++)
        {
            char c = codigo[i];
            bool esLetra = c >= 'A' && c <= 'Z';
            bool esNumero = c >= '0' && c <= '9';
            if (!esLetra && !esNumero)
                return false;
        }
        return true;
    }
}