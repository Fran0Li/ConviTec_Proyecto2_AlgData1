(async function () {
  await Api.encabezado("pedidos");
  const mensaje = document.getElementById("mensaje");
  const id = Api.parametro("id");

  if (!id) {
    Api.mostrarMensaje(mensaje, "Falta indicar el pedido. Vuelva a la lista de pedidos y elija uno.");
    return;
  }

  try {
    const p = await Api.pedir("/api/pedidos/" + encodeURIComponent(id));
    const resumen = document.getElementById("resumen");
    resumen.replaceChildren(
      Api.el("h3", "Pedido " + p.id),
      Api.el("p", "Destino: " + p.destino),
      Api.el("p", "Registrado: " + Api.fecha(p.fechaCreacion)),
      Api.el("p", "Total: " + Api.moneda(p.montoTotal))
    );
    const estado = Api.el("p", "Estado: ");
    estado.appendChild(Api.etiqueta(p.estado));
    resumen.appendChild(estado);
    if (p.estado === "Cancelado" && p.motivoCancelacion)
      resumen.appendChild(Api.el("p", "Motivo: " + p.motivoCancelacion));
    if (p.fechaEntrega)
      resumen.appendChild(Api.el("p", "Entregado: " + Api.fecha(p.fechaEntrega)));

    Api.tabla(document.getElementById("lineas"), [
      { titulo: "Producto", valor: function (d) { return d.codigoProducto + " · " + d.nombreProducto; } },
      { titulo: "Sede que lo envía", valor: function (d) { return d.codigoSedeOrigen; } },
      { titulo: "Cantidad", numero: true, valor: function (d) { return d.cantidad; } },
      { titulo: "Precio", numero: true, valor: function (d) { return Api.moneda(d.precioUnitario); } },
      { titulo: "Subtotal", numero: true, valor: function (d) { return Api.moneda(d.subtotal); } }
    ], p.detalles, "El pedido no tiene líneas.");
  } catch (e) {
    Api.mostrarMensaje(mensaje, e.message);
  }
})();
