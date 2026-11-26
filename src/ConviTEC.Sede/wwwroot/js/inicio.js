(async function () {
  await Api.encabezado("inicio");
  const mensaje = document.getElementById("mensaje");

  try {
    const sede = await Api.pedir("/api/sede");
    document.getElementById("datos-sede").textContent =
      sede.nombre + " (" + sede.codigo + "), ubicada en " + sede.ubicacion + ".";
  } catch (e) {
    Api.mostrarMensaje(mensaje, e.message);
  }

  try {
    const destinos = await Api.pedir("/api/destinos");
    // Campos de DestinoDisponible (Core): nombre, alcanzable y tiempo. El cálculo lo hizo el servidor.
    Api.tabla(document.getElementById("destinos"), [
      { titulo: "Destino", valor: function (d) { return d.nombre; } },
      {
        titulo: "Se puede llegar",
        valor: function (d) { return Api.el("span", d.alcanzable ? "Sí" : "No", "etiqueta " + (d.alcanzable ? "si" : "no")); }
      },
      { titulo: "Tiempo", numero: true, valor: function (d) { return d.alcanzable ? d.tiempo + " min" : "—"; } }
    ], destinos, "No hay destinos configurados.");
  } catch (e) {
    Api.mostrarMensaje(mensaje, e.message);
  }
})();
