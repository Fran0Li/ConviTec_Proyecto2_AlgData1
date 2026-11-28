// Página de entregas. Solo pide datos a la API y los muestra: el recorrido, el siguiente punto
// y las ventas los calcula el servidor (ServicioEntregas). Aquí no se suma ni se compara nada (PR-17).
(async function () {
  await Api.encabezado("entregas");

  const mensaje = document.getElementById("mensaje");
  const entradaSalida = document.getElementById("salida");
  const btnSiguiente = document.getElementById("btnSiguiente");
  const btnTodo = document.getElementById("btnTodo");

  // Lo que devolvió el servidor en el último paso. Se le vuelve a mandar tal cual.
  let salida = null;
  let desde = null;
  let acumulado = 0;
  let realizadas = [];

  const columnasPlan = [
    { titulo: "Destino", valor: function (p) { return p.destino; } },
    { titulo: "Camino", valor: function (p) { return p.camino.join(" → "); } },
    { titulo: "Tramo (min)", numero: true, valor: function (p) { return p.tiempoTramo; } },
    { titulo: "Acumulado (min)", numero: true, valor: function (p) { return p.tiempoAcumulado; } },
    { titulo: "Pedidos", valor: function (p) { return p.idsPedidos.join(", "); } }
  ];

  const columnasRealizadas = [
    { titulo: "Destino", valor: function (f) { return f.parada.destino; } },
    { titulo: "Camino", valor: function (f) { return f.parada.camino.join(" → "); } },
    { titulo: "Acumulado (min)", numero: true, valor: function (f) { return f.parada.tiempoAcumulado; } },
    { titulo: "Ventas", valor: function (f) {
        return f.ventas.map(function (v) { return v.id + " (" + Api.moneda(v.montoTotal) + ")"; }).join(", ");
      } }
  ];

  function consulta() {
    return salida ? "?salida=" + encodeURIComponent(salida) : "";
  }

  async function cargarPlan() {
    mensaje.replaceChildren();
    salida = entradaSalida.value || null;
    try {
      const recorrido = await Api.pedir("/api/entregas/plan" + consulta());
      salida = recorrido.fechaSalida;
      desde = null;
      acumulado = 0;
      realizadas = [];
      mostrarRealizadas();

      document.getElementById("resumenPlan").textContent =
        "Salida " + Api.fecha(recorrido.fechaSalida) + " desde " + recorrido.ubicacionInicial + " · " +
        recorrido.cantidadPedidosEntregados + " pedidos · " + recorrido.tiempoTotal + " min en total";

      const aviso = document.getElementById("avisoPlan");
      aviso.replaceChildren();
      if (recorrido.idsPedidosNoAlcanzables.length > 0) {
        Api.mostrarMensaje(aviso, "Sin ruta desde la sede (no se entregan): " +
          recorrido.idsPedidosNoAlcanzables.join(", "), "aviso");
      }

      Api.tabla(document.getElementById("plan"), columnasPlan, recorrido.paradas,
        "No hay pedidos pendientes para esta salida.");
      btnSiguiente.disabled = recorrido.paradas.length === 0;
      btnTodo.disabled = recorrido.paradas.length === 0;
    } catch (e) {
      Api.mostrarMensaje(mensaje, e.message);
    }
  }

  function mostrarRealizadas() {
    document.getElementById("ubicacion").textContent = desde
      ? "Ubicación actual del vehículo: " + desde + " · " + acumulado + " min desde la salida"
      : "El vehículo está en la sede.";
    Api.tabla(document.getElementById("realizadas"), columnasRealizadas, realizadas,
      "Todavía no se ha entregado nada en esta salida.");
  }

  async function entregarSiguiente() {
    mensaje.replaceChildren();
    let ruta = "/api/entregas/siguiente?salida=" + encodeURIComponent(salida) + "&acumulado=" + acumulado;
    if (desde) ruta += "&desde=" + encodeURIComponent(desde);
    try {
      const paso = await Api.pedir(ruta, { method: "POST" });
      if (paso.terminado) {
        Api.mostrarMensaje(mensaje, "No quedan entregas alcanzables en esta salida.", "aviso");
        btnSiguiente.disabled = true;
        return;
      }
      desde = paso.parada.destino;
      acumulado = paso.parada.tiempoAcumulado;
      realizadas.push(paso);
      btnTodo.disabled = true;   // ya empezó paso a paso: el resto se entrega desde aquí
      mostrarRealizadas();
    } catch (e) {
      Api.mostrarMensaje(mensaje, e.message);
    }
  }

  async function entregarTodo() {
    mensaje.replaceChildren();
    try {
      const resultado = await Api.pedir("/api/entregas" + consulta(), { method: "POST" });
      realizadas = resultado.recorrido.paradas.map(function (parada) {
        return {
          parada: parada,
          ventas: resultado.ventas.filter(function (v) { return parada.idsPedidos.indexOf(v.idPedido) !== -1; })
        };
      });
      const ultima = resultado.recorrido.paradas[resultado.recorrido.paradas.length - 1];
      desde = ultima ? ultima.destino : null;
      acumulado = resultado.recorrido.tiempoTotal;
      mostrarRealizadas();
      btnSiguiente.disabled = true;
      btnTodo.disabled = true;
      Api.mostrarMensaje(mensaje, "Recorrido entregado: " + resultado.ventas.length + " ventas registradas.", "aviso");
    } catch (e) {
      Api.mostrarMensaje(mensaje, e.message);
    }
  }

  document.getElementById("btnCargar").addEventListener("click", cargarPlan);
  btnSiguiente.addEventListener("click", entregarSiguiente);
  btnTodo.addEventListener("click", entregarTodo);

  await cargarPlan();
})();