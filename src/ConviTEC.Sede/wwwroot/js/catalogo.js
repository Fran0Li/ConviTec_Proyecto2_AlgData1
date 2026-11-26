(async function () {
  await Api.encabezado("catalogo");
  try {
    // GET /api/productos todavía no existe (depende del AVL del rol A): hasta entonces sale el aviso de "no disponible".
    const productos = await Api.pedir("/api/productos");
    Api.tabla(document.getElementById("catalogo"), [
      { titulo: "Código", valor: function (p) { return p.codigo; } },
      { titulo: "Producto", valor: function (p) { return p.nombre; } },
      { titulo: "Categoría", valor: function (p) { return p.categoria; } },
      { titulo: "Precio", numero: true, valor: function (p) { return Api.moneda(p.precio); } }
    ], productos, "El catálogo está vacío.");
  } catch (e) {
    Api.mostrarMensaje(document.getElementById("mensaje"), e.message, "aviso");
  }
})();
