// Utilidades comunes del frontend de ConviTEC.
// Regla PR-17: aquí NO hay lógica del negocio. Solo se piden datos a la API, se muestran
// y se da formato a moneda y fechas. Todo cálculo y toda validación viven en el servidor.
const Api = (function () {
  "use strict";

  // Páginas del menú. Las que aún no tienen endpoint avisan que no están disponibles (ver pedir).
  const PAGINAS = [
    { id: "inicio", texto: "Inicio", url: "index.html" },
    { id: "pedidos", texto: "Pedidos", url: "pedidos.html" },
    { id: "catalogo", texto: "Catálogo", url: "catalogo.html" },
    { id: "inventario", texto: "Inventario", url: "inventario.html" },
    { id: "entregas", texto: "Entregas", url: "entregas.html" }
    
  ];

  // Pide datos a la API y devuelve el JSON. Si algo sale mal lanza un Error con un mensaje en español
  // que se puede mostrar tal cual al dependiente.
  async function pedir(ruta, opciones) {
    let respuesta;
    try {
      respuesta = await fetch(ruta, opciones);
    } catch (e) {
      throw new Error("No se pudo conectar con la sede. Revise que esté encendida e intente de nuevo.");
    }

    if (respuesta.status === 404 && !respuesta.headers.get("content-type")?.includes("json")) {
      throw new Error("Esta función todavía no está disponible en la sede.");
    }

    let cuerpo = null;
    const texto = await respuesta.text();
    if (texto) {
      try { cuerpo = JSON.parse(texto); } catch (e) { cuerpo = null; }
    }

    if (!respuesta.ok) {
      // La API responde { "mensaje": "..." } en español (RF-12, PR-16).
      throw new Error(cuerpo && cuerpo.mensaje ? cuerpo.mensaje : "La sede respondió con un error (" + respuesta.status + ").");
    }
    return cuerpo;
  }

  // Crea un elemento con texto. Se usa textContent y nunca innerHTML: lo que venga de la API no se interpreta como HTML.
  function el(etiqueta, texto, clase) {
    const nodo = document.createElement(etiqueta);
    if (texto !== undefined && texto !== null) nodo.textContent = texto;
    if (clase) nodo.className = clase;
    return nodo;
  }

  // Encabezado con nombre de la sede y menú. Se llama una vez por página: Api.encabezado("pedidos").
  async function encabezado(paginaActual) {
    const cabecera = document.querySelector("header.sede");
    if (!cabecera) return;
    cabecera.replaceChildren();

    const titulo = el("h1", "ConviTEC");
    const subtitulo = el("div", "", "ubicacion");
    cabecera.append(titulo, subtitulo);

    const menu = el("nav", "", "menu");
    PAGINAS.forEach(function (p) {
      const enlace = el("a", p.texto, p.id === paginaActual ? "activo" : "");
      enlace.href = p.url;
      menu.appendChild(enlace);
    });
    cabecera.appendChild(menu);

    try {
      const sede = await pedir("/api/sede");
      titulo.textContent = "ConviTEC · Sede " + sede.nombre;
      subtitulo.textContent = "Código " + sede.codigo + " · " + sede.ubicacion;
      document.title = "ConviTEC · " + sede.nombre;
    } catch (e) {
      subtitulo.textContent = e.message;
    }
  }

  // Muestra un mensaje de error o aviso dentro del elemento indicado.
  function mostrarMensaje(contenedor, texto, tipo) {
    contenedor.replaceChildren(el("div", texto, "mensaje " + (tipo || "error")));
  }

  // Dibuja una tabla. columnas: [{ titulo, valor: fila => texto | Node, numero: bool }].
  // Si no hay filas muestra el texto de "vacio".
  function tabla(contenedor, columnas, filas, vacio) {
    contenedor.replaceChildren();
    if (!filas || filas.length === 0) {
      contenedor.appendChild(el("p", vacio || "No hay datos para mostrar.", "vacio"));
      return;
    }
    const t = document.createElement("table");
    const cabecera = document.createElement("tr");
    columnas.forEach(function (c) { cabecera.appendChild(el("th", c.titulo, c.numero ? "numero" : "")); });
    const thead = document.createElement("thead");
    thead.appendChild(cabecera);
    t.appendChild(thead);

    const cuerpo = document.createElement("tbody");
    filas.forEach(function (fila) {
      const tr = document.createElement("tr");
      columnas.forEach(function (c) {
        const valor = c.valor(fila);
        const td = el("td", "", c.numero ? "numero" : "");
        if (valor instanceof Node) td.appendChild(valor); else td.textContent = valor;
        tr.appendChild(td);
      });
      cuerpo.appendChild(tr);
    });
    t.appendChild(cuerpo);
    contenedor.appendChild(t);
  }

  // Solo formato de presentación (no es lógica del negocio).
  const formatoMoneda = new Intl.NumberFormat("es-CR", { style: "currency", currency: "CRC", maximumFractionDigits: 0 });
  function moneda(valor) { return formatoMoneda.format(valor); }

  function fecha(texto) {
    if (!texto) return "";
    const f = new Date(texto);
    if (isNaN(f.getTime())) return texto;
    return f.toLocaleString("es-CR", { dateStyle: "short", timeStyle: "short" });
  }

  function etiqueta(texto) { return el("span", texto, "etiqueta " + texto); }

  function parametro(nombre) { return new URLSearchParams(window.location.search).get(nombre); }

  return { pedir, el, encabezado, mostrarMensaje, tabla, moneda, fecha, etiqueta, parametro };
})();
