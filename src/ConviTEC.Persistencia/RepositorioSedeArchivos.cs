using System;
using System.IO;
using ConviTEC.Core.Contratos;
using ConviTEC.Core.Grafo;
using ConviTEC.Core.Mensajes;
using ConviTEC.Core.Modelos;

namespace ConviTEC.Persistencia;

// Implementa IRepositorioSede con los archivos JSON de data/<CÓDIGO>/ (documento de diseño, 5.10).
// Cada sede crea uno sobre su propia carpeta y nunca toca la de otra (RNF-12).
// Todo el trabajo de leer y escribir lo hace ArchivoJson; aquí se decide qué archivo es cuál y se
// revisa que lo leído tenga sentido, para reportar un archivo dañado con su nombre y la entrada que falla.
public class RepositorioSedeArchivos : IRepositorioSede
{
    public const string ArchivoRutas = "rutas.json";
    public const string ArchivoProductos = "productos.json";
    public const string ArchivoInventario = "inventario.json";
    public const string ArchivoInventarioInicial = "inventario.inicial.json";
    public const string ArchivoReservas = "reservas.json";
    public const string ArchivoPedidos = "pedidos.json";
    public const string ArchivoVentas = "ventas.json";
    public const string ArchivoInventarioGlobal = "inventario_global.json";
    public const string ArchivoContadores = "contadores.json";

    private readonly string carpeta;

    public RepositorioSedeArchivos(string carpeta)
    {
        if (string.IsNullOrWhiteSpace(carpeta))
            throw new ArgumentException("Falta la carpeta de datos de la sede.", nameof(carpeta));
        this.carpeta = Path.GetFullPath(carpeta);
    }

    // Carpeta de datos de la sede, como ruta completa.
    public string Carpeta => carpeta;

    // Ruta completa de un archivo de la sede (por ejemplo, para el registro de reservas).
    public string RutaDe(string nombreArchivo)
    {
        return Path.Combine(carpeta, nombreArchivo);
    }

    // ---------- Solo lectura: rutas y productos ----------

    public Ruta[] LeerRutas()
    {
        Ruta[] rutas = LeerObligatorio<Ruta>(ArchivoRutas);
        if (rutas.Length == 0)
            throw new FormatException("El archivo " + RutaDe(ArchivoRutas) + " no tiene ninguna ruta.");
        return rutas;
    }

    public Producto[] LeerProductos()
    {
        Producto[] productos = LeerObligatorio<Producto>(ArchivoProductos);
        if (productos.Length == 0)
            throw new FormatException("El archivo " + RutaDe(ArchivoProductos) + " no tiene ningún producto.");

        for (int i = 0; i < productos.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(productos[i].Codigo))
                throw Problema(ArchivoProductos, i, "el producto no tiene código");
            if (string.IsNullOrWhiteSpace(productos[i].Nombre))
                throw Problema(ArchivoProductos, i, "el producto " + productos[i].Codigo + " no tiene nombre");
            if (productos[i].Precio < 0)
                throw Problema(ArchivoProductos, i, "el producto " + productos[i].Codigo + " tiene precio negativo");
        }
        return productos;
    }

    // ---------- Inventario ----------

    public LineaInventario[] LeerInventario()
    {
        // Primer arranque: todavía no existe inventario.json, se parte del inventario inicial del repositorio.
        string archivo = File.Exists(RutaDe(ArchivoInventario)) ? ArchivoInventario : ArchivoInventarioInicial;
        LineaInventario[] lineas = ArchivoJson.LeerArreglo<LineaInventario>(RutaDe(archivo));

        for (int i = 0; i < lineas.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lineas[i].Codigo))
                throw Problema(archivo, i, "la línea no tiene código de producto");
            if (lineas[i].Cantidad < 0)
                throw Problema(archivo, i, lineas[i].Codigo + " tiene cantidad negativa");
            if (lineas[i].Reservado < 0)
                throw Problema(archivo, i, lineas[i].Codigo + " tiene reservado negativo");
            if (lineas[i].Reservado > lineas[i].Cantidad)
                throw Problema(archivo, i, lineas[i].Codigo + " tiene más unidades reservadas que existencias");
        }
        return lineas;
    }

    public void GuardarInventario(LineaInventario[] lineas)
    {
        ArchivoJson.GuardarArreglo(RutaDe(ArchivoInventario), lineas);
    }

    // ---------- Pedidos y ventas ----------

    public Pedido[] LeerPedidos()
    {
        Pedido[] pedidos = ArchivoJson.LeerArreglo<Pedido>(RutaDe(ArchivoPedidos));

        for (int i = 0; i < pedidos.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(pedidos[i].Id))
                throw Problema(ArchivoPedidos, i, "el pedido no tiene Id");
            RevisarDetalles(ArchivoPedidos, i, pedidos[i].Detalles);
        }
        return pedidos;
    }

    public void GuardarPedidos(Pedido[] pedidos)
    {
        ArchivoJson.GuardarArreglo(RutaDe(ArchivoPedidos), pedidos);
    }

    public Venta[] LeerVentas()
    {
        Venta[] ventas = ArchivoJson.LeerArreglo<Venta>(RutaDe(ArchivoVentas));

        for (int i = 0; i < ventas.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(ventas[i].Id))
                throw Problema(ArchivoVentas, i, "la venta no tiene Id");
            RevisarDetalles(ArchivoVentas, i, ventas[i].Detalles);
        }
        return ventas;
    }

    public void GuardarVentas(Venta[] ventas)
    {
        ArchivoJson.GuardarArreglo(RutaDe(ArchivoVentas), ventas);
    }

    // ---------- Inventario global ----------

    public InventarioDeSede[] LeerInventarioGlobal()
    {
        InventarioDeSede[] copia = ArchivoJson.LeerArreglo<InventarioDeSede>(RutaDe(ArchivoInventarioGlobal));

        for (int i = 0; i < copia.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(copia[i].CodigoSede))
                throw Problema(ArchivoInventarioGlobal, i, "falta el código de la sede");
            if (copia[i].Lineas == null)
                throw Problema(ArchivoInventarioGlobal, i, "la sede " + copia[i].CodigoSede + " no tiene líneas");
            for (int j = 0; j < copia[i].Lineas.Length; j++)
            {
                LineaMensaje linea = copia[i].Lineas[j];
                if (linea == null || string.IsNullOrWhiteSpace(linea.Codigo))
                    throw Problema(ArchivoInventarioGlobal, i, "la sede " + copia[i].CodigoSede + " tiene una línea sin código");
            }
        }
        return copia;
    }

    public void GuardarInventarioGlobal(InventarioDeSede[] copia)
    {
        ArchivoJson.GuardarArreglo(RutaDe(ArchivoInventarioGlobal), copia);
    }

    // ---------- Contadores ----------

    // Como todos los archivos son arreglos JSON (5.10), contadores.json guarda un arreglo de una sola entrada.
    public Contadores LeerContadores()
    {
        Contadores[] guardados = ArchivoJson.LeerArreglo<Contadores>(RutaDe(ArchivoContadores));
        if (guardados.Length == 0)
            return new Contadores();

        if (guardados[0].UltimoPedido < 0 || guardados[0].UltimaVenta < 0)
            throw Problema(ArchivoContadores, 0, "los contadores no pueden ser negativos");
        return guardados[0];
    }

    public void GuardarContadores(Contadores contadores)
    {
        if (contadores == null)
            throw new ArgumentNullException(nameof(contadores));
        ArchivoJson.GuardarArreglo(RutaDe(ArchivoContadores), new[] { contadores });
    }

    // ---------- Ayudas ----------

    // Lee un archivo que la sede no puede arrancar sin él. Si falta, dice cuál y dónde se esperaba.
    private T[] LeerObligatorio<T>(string nombreArchivo)
    {
        string ruta = RutaDe(nombreArchivo);
        if (!File.Exists(ruta))
            throw new FileNotFoundException("Falta el archivo " + ruta + ".", ruta);
        return ArchivoJson.LeerArreglo<T>(ruta);
    }

    private void RevisarDetalles(string archivo, int posicion, DetallePedido[]? detalles)
    {
        if (detalles == null)
            throw Problema(archivo, posicion, "no tiene detalle de productos");
        for (int i = 0; i < detalles.Length; i++)
        {
            if (detalles[i] == null)
                throw Problema(archivo, posicion, "tiene una línea de detalle vacía");
        }
    }

    // Arma el error con el archivo y el número de entrada (desde 1) para que se encuentre fácil.
    private FormatException Problema(string nombreArchivo, int posicion, string problema)
    {
        return new FormatException(
            "El archivo " + RutaDe(nombreArchivo) + ", entrada " + (posicion + 1) + ": " + problema + ".");
    }
}