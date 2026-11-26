(async function () {
  await Api.encabezado("inventario");
  try {
    // GET /api/inventario todavía no existe (depende de las estructuras del rol A).
    const lineas = await Api.pedir("/api/inventario");
    Api.tabla(document.getElementById("inventario"), [
      { titulo: "Código", valor: function (l) { return l.codigo; } },
      { titulo: "Existencias", numero: true, valor: function (l) { return l.cantidad; } },
      { titulo: "Reservado", numero: true, valor: function (l) { return l.reservado; } }
    ], lineas, "No hay productos en el inventario.");
  } catch (e) {
    Api.mostrarMensaje(document.getElementById("mensaje"), e.message, "aviso");
  }
})();
