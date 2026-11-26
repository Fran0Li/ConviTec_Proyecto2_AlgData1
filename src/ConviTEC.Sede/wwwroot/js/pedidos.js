(async function () {
  await Api.encabezado("pedidos");
  const mensaje = document.getElementById("mensaje");

  function enlaceAlPedido(p) {
    const a = Api.el("a", p.id);
    a.href = "pedido.html?id=" + encodeURIComponent(p.id);
    return a;
  }

  const columnas = [
    { titulo: "Pedido", valor: enlaceAlPedido },
    { titulo: "Fecha", valor: function (p) { return Api.fecha(p.fechaCreacion); } },
    { titulo: "Destino", valor: function (p) { return p.destino; } },
    { titulo: "Estado", valor: function (p) { return Api.etiqueta(p.estado); } },
    { titulo: "Monto", numero: true, valor: function (p) { return Api.moneda(p.montoTotal); } }
  ];

  try {
    // Los dos listados los arma la API; aquí solo se dibujan.
    const pendientes = await Api.pedir("/api/pedidos/pendientes");
    Api.tabla(document.getElementById("pendientes"), columnas, pendientes, "No hay pedidos pendientes de entrega.");

    const historial = await Api.pedir("/api/pedidos");
    Api.tabla(document.getElementById("historial"), columnas, historial, "Todavía no hay pedidos registrados.");
  } catch (e) {
    Api.mostrarMensaje(mensaje, e.message);
  }
})();
