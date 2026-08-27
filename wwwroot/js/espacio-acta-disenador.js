/* ═══════════════════════════════════════════════════════════════════════════
   Editor de actas — Mi espacio corporativo

   Pensado para que lo use cualquiera, no solo quien sepa de sistemas:

   • Se escribe sobre el documento, no sobre un formulario.
   • Un dato que cambia se ve como una ficha con su nombre ("Nombre completo"),
     nunca como {{codigo}}. Al guardar se traduce a {{codigo}} y al abrir se
     vuelve a pintar como ficha.
   • Crear un dato lo mete en el texto de una vez: no hay un segundo paso que
     adivinar.
   • Arriba siempre está lo que falta, y se puede guardar a medias.

   El estado vive en un solo objeto con la misma forma que espera el servidor.
   ═══════════════════════════════════════════════════════════════════════════ */

(() => {
  "use strict";

  const contenedorBloques = document.getElementById("actaBloques");
  if (!contenedorBloques) return;

  // ── Catálogos ─────────────────────────────────────────────────────────────

  const TIPOS_BLOQUE = {
    Titulo: { etiqueta: "Título", icono: "bi-type-h2", pista: "Nombre de la sección" },
    Parrafo: { etiqueta: "Párrafo", icono: "bi-text-paragraph", pista: "Escribe aquí…" },
    Lista: { etiqueta: "Lista", icono: "bi-list-ul", pista: "Un punto por línea" },
    Nota: { etiqueta: "Aviso", icono: "bi-exclamation-square", pista: "Algo que debe resaltar" },
    Datos: { etiqueta: "Cuadro de datos", icono: "bi-table", pista: "" },
    Separador: { etiqueta: "Línea", icono: "bi-dash-lg", pista: "" }
  };

  /** Nunca reventar por un tipo que no se conozca: se trata como párrafo. */
  const infoBloque = (tipo) => TIPOS_BLOQUE[tipo] || TIPOS_BLOQUE.Parrafo;

  const leerJson = (id, porDefecto) => {
    const nodo = document.getElementById(id);
    if (!nodo) return porDefecto;
    try {
      return JSON.parse(nodo.textContent) ?? porDefecto;
    } catch (error) {
      console.error("No se pudo leer " + id, error);
      return porDefecto;
    }
  };

  const TIPOS_CAMPO = leerJson("actaTiposCampo", []);
  const MODELOS = leerJson("actaModelosDatos", []);
  const DEL_SISTEMA = leerJson("actaMarcadoresSistema", []);
  const CONFIG = leerJson("actaConfig", { elegirModelo: false, publicada: false });

  const infoTipo = (tipo) =>
    TIPOS_CAMPO.find((x) => x.tipo === tipo) ||
    { tipo: tipo, etiqueta: tipo, icono: "bi-input-cursor-text", descripcion: "", admiteOpciones: false };

  // ── Estado ────────────────────────────────────────────────────────────────

  const vacio = {
    id: null,
    nombre: "",
    descripcion: "",
    icono: "bi-file-earmark-text-fill",
    tituloActa: "",
    numerarTitulos: true,
    campoNombre: "",
    campoDocumento: "",
    campoCorreo: "",
    campoUsuario: "",
    campos: [],
    bloques: [],
    firmas: []
  };

  const normalizarDefinicion = (origen) => ({
    id: origen.id ?? null,
    nombre: origen.nombre ?? "",
    descripcion: origen.descripcion ?? "",
    icono: origen.icono ?? "bi-file-earmark-text-fill",
    tituloActa: origen.tituloActa ?? "",
    numerarTitulos: origen.numerarTitulos !== false,
    campoNombre: origen.campoNombre ?? "",
    campoDocumento: origen.campoDocumento ?? "",
    campoCorreo: origen.campoCorreo ?? "",
    campoUsuario: origen.campoUsuario ?? "",
    campos: (origen.campos ?? []).map((campo) => ({
      clave: campo.clave ?? "",
      etiqueta: campo.etiqueta ?? "",
      tipo: campo.tipo ?? "Texto",
      requerido: campo.requerido !== false,
      placeholder: campo.placeholder ?? "",
      ayuda: campo.ayuda ?? "",
      visibleEnActa: campo.visibleEnActa !== false,
      opciones: (campo.opciones ?? []).map((o) => ({ valor: o.valor ?? "", etiqueta: o.etiqueta ?? "" }))
    })),
    bloques: (origen.bloques ?? []).map((bloque) => ({
      tipo: bloque.tipo ?? "Parrafo",
      texto: bloque.texto ?? "",
      campos: bloque.campos ?? []
    })),
    firmas: (origen.firmas ?? []).map((firma) => ({
      clave: firma.clave ?? "",
      rotulo: firma.rotulo ?? "",
      origen: firma.origen ?? "EnVivo",
      campoNombre: firma.campoNombre ?? "",
      campoDocumento: firma.campoDocumento ?? "",
      nombreFijo: firma.nombreFijo ?? "",
      cargoFijo: firma.cargoFijo ?? "",
      requerida: firma.requerida !== false
    }))
  });

  let estado = normalizarDefinicion(leerJson("actaDefinicionInicial", null) || vacio);
  let publicada = CONFIG.publicada === true;
  let arrancado = false;
  let hayCambios = false;

  // ── Utilidades de DOM ─────────────────────────────────────────────────────

  const el = (etiqueta, clase, texto) => {
    const nodo = document.createElement(etiqueta);
    if (clase) nodo.className = clase;
    if (texto !== undefined && texto !== null) nodo.textContent = texto;
    return nodo;
  };

  const icono = (clase) => {
    const i = document.createElement("i");
    i.className = "bi " + clase;
    i.setAttribute("aria-hidden", "true");
    return i;
  };

  const boton = (clase, claseIcono, texto, titulo) => {
    const b = document.createElement("button");
    b.type = "button";
    if (clase) b.className = clase;
    if (claseIcono) b.appendChild(icono(claseIcono));
    if (texto) b.appendChild(document.createTextNode(texto));
    if (titulo) {
      b.title = titulo;
      b.setAttribute("aria-label", titulo);
    }
    return b;
  };

  const abrirModal = (modal) => {
    modal.classList.add("is-open");
    document.body.style.overflow = "hidden";
  };

  const cerrarModal = (modal) => {
    modal.classList.remove("is-open");
    if (!document.querySelector(".espacio-modal.is-open")) {
      document.body.style.overflow = "";
    }
  };

  document.querySelectorAll("[data-modal-cerrar]").forEach((b) => {
    b.addEventListener("click", () => cerrarModal(b.closest(".espacio-modal")));
  });

  document.querySelectorAll(".espacio-modal").forEach((modal) => {
    modal.addEventListener("mousedown", (evento) => {
      // El modal de modelos es la puerta de entrada: no se cierra por fuera.
      if (evento.target === modal && modal.id !== "actaModalModelos") {
        cerrarModal(modal);
      }
    });
  });

  /** Clave estable para un dato. Se calcula una vez y no cambia al renombrarlo. */
  const claveLibre = (origen) => {
    const usadas = new Set(estado.campos.map((x) => x.clave));

    let base = (origen || "")
      .normalize("NFD")
      .replace(/[̀-ͯ]/g, "")
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "_")
      .replace(/^_+|_+$/g, "")
      .slice(0, 36);

    if (!base || !/^[a-z]/.test(base)) base = "dato" + (base ? "_" + base : "");

    let candidata = base;
    let sufijo = 2;
    while (usadas.has(candidata)) {
      candidata = base + "_" + sufijo;
      sufijo += 1;
    }
    return candidata;
  };

  const campoPorClave = (clave) => estado.campos.find((x) => x.clave === clave);

  // ── Texto ⇄ contenido editable ────────────────────────────────────────────
  //
  // En el editor los datos son fichas; al guardar vuelven a ser {{clave}} y la
  // negrita vuelve a ser **texto**, que es lo que entiende el servidor.

  const delSistema = (clave) => DEL_SISTEMA.find((x) => x.valor === clave);

  function fichaDato(clave) {
    const ficha = el("span", "acta-dato");
    ficha.contentEditable = "false";
    ficha.dataset.dato = clave;

    // La fecha, la ciudad y los datos de quien emite los pone el sistema solo.
    const sistema = delSistema(clave);
    if (sistema) {
      ficha.classList.add("acta-dato--sistema");
      ficha.textContent = sistema.etiqueta;
      ficha.title = "Lo pone el sistema al emitir el acta";
      return ficha;
    }

    const campo = campoPorClave(clave);
    ficha.textContent = campo ? campo.etiqueta || clave : "dato borrado";
    ficha.classList.toggle("acta-dato--roto", !campo);
    ficha.title = campo
      ? "Aquí va: " + campo.etiqueta
      : "Este dato ya no existe. Bórralo del texto.";
    return ficha;
  }

  function leerContenido(nodo) {
    let salida = "";

    nodo.childNodes.forEach((hijo) => {
      if (hijo.nodeType === Node.TEXT_NODE) {
        salida += hijo.nodeValue;
        return;
      }

      if (hijo.nodeType !== Node.ELEMENT_NODE) return;

      if (hijo.dataset && hijo.dataset.dato) {
        salida += "{{" + hijo.dataset.dato + "}}";
        return;
      }

      const etiqueta = hijo.tagName.toLowerCase();

      if (etiqueta === "br") {
        salida += "\n";
        return;
      }

      if (etiqueta === "strong" || etiqueta === "b") {
        const dentro = leerContenido(hijo).trim();
        salida += dentro ? "**" + dentro + "**" : "";
        return;
      }

      // El navegador envuelve cada línea nueva en un div al pulsar Enter.
      if (etiqueta === "div" || etiqueta === "p") {
        if (salida && !salida.endsWith("\n")) salida += "\n";
        salida += leerContenido(hijo);
        return;
      }

      salida += leerContenido(hijo);
    });

    return salida;
  }

  function pintarContenido(nodo, texto) {
    nodo.replaceChildren();

    const partes = (texto || "").split(/(\{\{[a-zA-Z0-9_]+\}\}|\*\*[^*\n]+\*\*)/g);

    partes.forEach((parte) => {
      if (!parte) return;

      const dato = parte.match(/^\{\{([a-zA-Z0-9_]+)\}\}$/);
      if (dato) {
        nodo.appendChild(fichaDato(dato[1]));
        return;
      }

      const negrita = parte.match(/^\*\*([^*\n]+)\*\*$/);
      if (negrita) {
        // Dentro de la negrita casi siempre hay un dato: se pinta con las mismas
        // reglas para que salga como ficha y no como {{codigo}}.
        const fuerte = document.createElement("strong");
        pintarContenido(fuerte, negrita[1]);
        nodo.appendChild(fuerte);
        return;
      }

      parte.split("\n").forEach((linea, indice) => {
        if (indice > 0) nodo.appendChild(document.createElement("br"));
        if (linea) nodo.appendChild(document.createTextNode(linea));
      });
    });
  }

  // ── Dónde estaba el cursor ────────────────────────────────────────────────

  let ultimoPunto = null;

  function recordarPunto() {
    const seleccion = window.getSelection();
    if (!seleccion || seleccion.rangeCount === 0) return;

    const rango = seleccion.getRangeAt(0);
    const nodo = rango.startContainer;
    const elemento = nodo.nodeType === Node.ELEMENT_NODE ? nodo : nodo.parentElement;
    const editable = elemento && elemento.closest("[data-editable]");

    if (editable) {
      ultimoPunto = { rango: rango.cloneRange(), editable: editable };
    }
  }

  document.addEventListener("selectionchange", recordarPunto);

  // ── El documento: bloques ─────────────────────────────────────────────────

  function pintarBloques() {
    contenedorBloques.replaceChildren();

    if (estado.bloques.length === 0) {
      const vacia = el("div", "acta-hoja-vacia");
      vacia.appendChild(icono("bi-pencil"));
      vacia.appendChild(el("strong", null, "El acta está en blanco"));
      vacia.appendChild(
        el("span", null, "Escribe el primer párrafo y ve agregando lo que necesites.")
      );

      const empezar = boton("espacio-btn espacio-btn--primary", "bi-plus-lg", "Escribir el primer párrafo");
      empezar.addEventListener("click", () => agregarBloque("Parrafo"));
      vacia.appendChild(empezar);

      contenedorBloques.appendChild(vacia);
      return;
    }

    estado.bloques.forEach((bloque, indice) => {
      contenedorBloques.appendChild(tarjetaBloque(bloque, indice));
    });
  }

  function tarjetaBloque(bloque, indice) {
    const tarjeta = el("div", "acta-bloque acta-bloque--" + bloque.tipo.toLowerCase());
    tarjeta.appendChild(barraBloque(bloque, indice));

    if (bloque.tipo === "Separador") {
      tarjeta.appendChild(el("hr", "acta-separador-vista"));
      return tarjeta;
    }

    if (bloque.tipo === "Datos") {
      tarjeta.appendChild(cuadroDeDatos(bloque));
      return tarjeta;
    }

    const editable = el("div", "acta-entrada acta-entrada--" + bloque.tipo.toLowerCase());
    editable.contentEditable = "true";
    editable.dataset.editable = String(indice);
    editable.setAttribute("role", "textbox");
    editable.setAttribute("aria-multiline", bloque.tipo === "Titulo" ? "false" : "true");
    editable.setAttribute("aria-label", infoBloque(bloque.tipo).etiqueta);
    editable.dataset.pista = infoBloque(bloque.tipo).pista;

    pintarContenido(editable, bloque.texto);

    editable.addEventListener("input", () => {
      bloque.texto = leerContenido(editable);
      editable.classList.toggle("is-vacio", editable.textContent.trim() === "");
      refrescarEstado();
    });

    // Pegar siempre como texto plano: si no, entra marcado del portapapeles.
    editable.addEventListener("paste", (evento) => {
      evento.preventDefault();
      const texto = (evento.clipboardData || window.clipboardData).getData("text/plain");
      document.execCommand("insertText", false, texto);
    });

    // Un título es una sola línea.
    if (bloque.tipo === "Titulo") {
      editable.addEventListener("keydown", (evento) => {
        if (evento.key === "Enter") evento.preventDefault();
      });
    }

    editable.classList.toggle("is-vacio", editable.textContent.trim() === "");
    tarjeta.appendChild(editable);

    if (bloque.tipo === "Lista") {
      tarjeta.appendChild(el("small", "acta-bloque__pista", "Cada línea sale con su propia viñeta."));
    }

    return tarjeta;
  }

  function barraBloque(bloque, indice) {
    const barra = el("div", "acta-bloque__barra");

    if (bloque.tipo !== "Separador" && bloque.tipo !== "Datos") {
      const tipo = document.createElement("select");
      tipo.className = "acta-bloque__tipo";
      tipo.title = "Cambiar el tipo de este texto";
      tipo.setAttribute("aria-label", "Tipo de texto");

      ["Parrafo", "Titulo", "Lista", "Nota"].forEach((clave) => {
        const opcion = document.createElement("option");
        opcion.value = clave;
        opcion.textContent = TIPOS_BLOQUE[clave].etiqueta;
        if (clave === bloque.tipo) opcion.selected = true;
        tipo.appendChild(opcion);
      });

      tipo.addEventListener("change", () => {
        bloque.tipo = tipo.value;
        pintarBloques();
        refrescarEstado();
      });

      barra.appendChild(tipo);
    } else {
      barra.appendChild(el("span", "acta-bloque__tipo-fijo", infoBloque(bloque.tipo).etiqueta));
    }

    const acciones = el("div", "acta-bloque__acciones");

    const subir = boton(null, "bi-arrow-up", null, "Subir");
    subir.disabled = indice === 0;
    subir.addEventListener("click", () => moverBloque(indice, -1));

    const bajar = boton(null, "bi-arrow-down", null, "Bajar");
    bajar.disabled = indice === estado.bloques.length - 1;
    bajar.addEventListener("click", () => moverBloque(indice, 1));

    const borrar = boton("is-danger", "bi-trash3", null, "Quitar");
    borrar.addEventListener("click", () => {
      estado.bloques.splice(indice, 1);
      pintarBloques();
      refrescarEstado();
    });

    [subir, bajar, borrar].forEach((b) => acciones.appendChild(b));
    barra.appendChild(acciones);

    return barra;
  }

  /**
   * Cuadro que imprime "Etiqueta: valor" con los datos marcados. Es la forma
   * ordenada de listar varios datos seguidos sin redactar una frase para cada uno.
   */
  function cuadroDeDatos(bloque) {
    const caja = el("div", "acta-cuadro");
    caja.appendChild(
      el("small", "acta-bloque__pista", "Marca los datos que quieres listar uno debajo del otro.")
    );

    if (estado.campos.length === 0) {
      caja.appendChild(
        el("small", "acta-bloque__pista", "Todavía no hay datos para listar. Crea uno con Agregar dato.")
      );
      return caja;
    }

    const lista = el("div", "acta-cuadro__opciones");

    estado.campos.forEach((campo) => {
      const etiqueta = el("label", "acta-switch");

      const marca = document.createElement("input");
      marca.type = "checkbox";
      marca.checked = (bloque.campos || []).includes(campo.clave);
      marca.addEventListener("change", () => {
        bloque.campos = marca.checked
          ? (bloque.campos || []).concat(campo.clave)
          : (bloque.campos || []).filter((clave) => clave !== campo.clave);
        refrescarEstado();
      });

      etiqueta.appendChild(marca);
      etiqueta.appendChild(el("span", null, campo.etiqueta || campo.clave));
      lista.appendChild(etiqueta);
    });

    caja.appendChild(lista);
    return caja;
  }

  function moverBloque(indice, salto) {
    const destino = indice + salto;
    if (destino < 0 || destino >= estado.bloques.length) return;
    const [bloque] = estado.bloques.splice(indice, 1);
    estado.bloques.splice(destino, 0, bloque);
    pintarBloques();
  }

  /** Índice del bloque donde está el cursor, o el último. */
  function bloqueActivo() {
    if (ultimoPunto && document.body.contains(ultimoPunto.editable)) {
      return Number(ultimoPunto.editable.dataset.editable);
    }
    return estado.bloques.length - 1;
  }

  function agregarBloque(tipo) {
    const posicion = bloqueActivo() + 1;
    estado.bloques.splice(posicion, 0, { tipo: tipo, texto: "", campos: [] });
    pintarBloques();
    refrescarEstado();

    const nuevo = contenedorBloques.querySelector('[data-editable="' + posicion + '"]');
    if (nuevo) {
      nuevo.focus();
      nuevo.scrollIntoView({ behavior: "smooth", block: "center" });
    }
  }

  document.querySelectorAll("[data-agregar]").forEach((b) => {
    b.addEventListener("click", () => agregarBloque(b.dataset.agregar));
  });

  document.querySelector('[data-formato="negrita"]').addEventListener("click", () => {
    if (!ultimoPunto || !document.body.contains(ultimoPunto.editable)) {
      avisarEnBarra("Selecciona primero las palabras que quieres resaltar.");
      return;
    }

    ultimoPunto.editable.focus();
    document.execCommand("bold");
    ultimoPunto.editable.dispatchEvent(new Event("input", { bubbles: true }));
  });

  // ── Insertar un dato en el texto ──────────────────────────────────────────

  function insertarDatoEnTexto(clave) {
    let punto = ultimoPunto && document.body.contains(ultimoPunto.editable) ? ultimoPunto : null;

    // Si nunca puso el cursor en el texto, el dato va al final: nunca se pierde.
    if (!punto) {
      if (estado.bloques.filter((b) => b.tipo !== "Separador").length === 0) {
        estado.bloques.push({ tipo: "Parrafo", texto: "", campos: [] });
        pintarBloques();
      }

      const editables = contenedorBloques.querySelectorAll("[data-editable]");
      const ultimo = editables[editables.length - 1];
      if (!ultimo) return;

      const rango = document.createRange();
      rango.selectNodeContents(ultimo);
      rango.collapse(false);
      punto = { rango: rango, editable: ultimo };
    }

    const ficha = fichaDato(clave);
    const espacio = document.createTextNode(" ");

    const rango = punto.rango;
    rango.deleteContents();
    rango.insertNode(espacio);
    rango.insertNode(ficha);

    // Si el dato cae pegado a la palabra anterior, se separa: al imprimir el acta
    // quedaría "descritos.27 de agosto" en vez de "descritos. 27 de agosto".
    const anterior = ficha.previousSibling;
    if (
      anterior &&
      anterior.nodeType === Node.TEXT_NODE &&
      anterior.nodeValue.length > 0 &&
      !/\s$/.test(anterior.nodeValue)
    ) {
      anterior.nodeValue += " ";
    }

    // Dejar el cursor justo después del dato para poder seguir escribiendo.
    const seleccion = window.getSelection();
    const despues = document.createRange();
    despues.setStartAfter(espacio);
    despues.collapse(true);
    seleccion.removeAllRanges();
    seleccion.addRange(despues);

    punto.editable.focus();
    punto.editable.dispatchEvent(new Event("input", { bubbles: true }));
    ultimoPunto = { rango: despues.cloneRange(), editable: punto.editable };

    punto.editable.scrollIntoView({ behavior: "smooth", block: "center" });
  }

  /** Renombrar un dato: las fichas ya puestas en el texto cambian con él. */
  function refrescarFichas(clave) {
    if (delSistema(clave)) return;

    const campo = campoPorClave(clave);

    contenedorBloques.querySelectorAll('[data-dato="' + clave + '"]').forEach((ficha) => {
      ficha.textContent = campo ? campo.etiqueta || clave : "dato borrado";
      ficha.classList.toggle("acta-dato--roto", !campo);
      ficha.title = campo ? "Aquí va: " + campo.etiqueta : "Este dato ya no existe. Bórralo del texto.";
    });
  }

  // ── Modal: crear o insertar un dato ───────────────────────────────────────

  const modalDato = document.getElementById("actaModalDato");
  const datoEtiqueta = document.getElementById("datoEtiqueta");
  const datoTipo = document.getElementById("datoTipo");
  const datoEjemplo = document.querySelector('[data-ejemplo-de="datoTipo"]');
  const datoOpcionesCaja = document.getElementById("datoOpcionesCaja");
  const datoOpciones = document.getElementById("datoOpciones");
  const datoAviso = document.getElementById("datoAviso");
  const datoGuardar = document.getElementById("datoGuardar");
  const datoOculto = document.getElementById("datoOculto");
  const datoOcultoCaja = document.getElementById("datoOcultoCaja");
  const datosExistentes = document.getElementById("datosExistentes");
  const datosDelSistema = document.getElementById("datosDelSistema");

  let datoEnEdicion = null;
  let opcionesTemporales = [];

  TIPOS_CAMPO.forEach((tipo) => {
    const opcion = document.createElement("option");
    opcion.value = tipo.tipo;
    opcion.textContent = tipo.etiqueta;
    datoTipo.appendChild(opcion);
  });

  const refrescarEjemplo = () => {
    datoEjemplo.textContent = infoTipo(datoTipo.value).descripcion || "";
    const admite = infoTipo(datoTipo.value).admiteOpciones;
    datoOpcionesCaja.hidden = !admite;
    if (admite && opcionesTemporales.length === 0) {
      opcionesTemporales = [""];
    }
    pintarOpciones();
  };

  function pintarOpciones() {
    datoOpciones.replaceChildren();

    opcionesTemporales.forEach((valor, indice) => {
      const fila = el("div", "acta-opcion");

      const entrada = document.createElement("input");
      entrada.type = "text";
      entrada.value = valor;
      entrada.maxLength = 120;
      entrada.placeholder = "Ej: Contrato a término fijo";
      entrada.addEventListener("input", () => {
        opcionesTemporales[indice] = entrada.value;
      });

      const quitar = boton("espacio-icon-btn espacio-icon-btn--danger", "bi-x-lg", null, "Quitar opción");
      quitar.addEventListener("click", () => {
        opcionesTemporales.splice(indice, 1);
        pintarOpciones();
      });

      fila.appendChild(entrada);
      fila.appendChild(quitar);
      datoOpciones.appendChild(fila);
    });
  }

  datoTipo.addEventListener("change", refrescarEjemplo);

  document.getElementById("datoAgregarOpcion").addEventListener("click", () => {
    opcionesTemporales.push("");
    pintarOpciones();
  });

  function abrirDato(campo) {
    datoEnEdicion = campo || null;
    datoAviso.hidden = true;

    datoEtiqueta.value = campo ? campo.etiqueta : "";
    datoTipo.value = campo ? campo.tipo : "Texto";
    datoOculto.checked = campo ? !campo.visibleEnActa : false;
    datoOcultoCaja.hidden = Boolean(campo);
    opcionesTemporales = campo ? campo.opciones.map((o) => o.etiqueta || o.valor) : [];

    document.getElementById("actaModalDatoTitulo").textContent = campo
      ? "Cambiar el dato"
      : "Agregar un dato al acta";

    datoGuardar.replaceChildren();
    datoGuardar.appendChild(icono(campo ? "bi-check-lg" : "bi-plus-lg"));
    datoGuardar.appendChild(document.createTextNode(campo ? "Guardar cambios" : "Insertar en el acta"));

    refrescarEjemplo();
    pintarDatosExistentes(campo);
    pintarDatosDelSistema(campo);

    abrirModal(modalDato);
    window.setTimeout(() => datoEtiqueta.focus(), 80);
  }

  function pintarDatosExistentes(enEdicion) {
    const lista = datosExistentes.querySelector("[data-lista]");
    lista.replaceChildren();

    const disponibles = estado.campos.filter((c) => c !== enEdicion && c.visibleEnActa);
    datosExistentes.hidden = Boolean(enEdicion) || disponibles.length === 0;
    if (datosExistentes.hidden) return;

    disponibles.forEach((campo) => {
      const ficha = boton("acta-ficha", infoTipo(campo.tipo).icono, campo.etiqueta || campo.clave);
      ficha.addEventListener("click", () => {
        cerrarModal(modalDato);
        insertarDatoEnTexto(campo.clave);
      });
      lista.appendChild(ficha);
    });
  }

  function pintarDatosDelSistema(enEdicion) {
    const lista = datosDelSistema.querySelector("[data-lista]");
    lista.replaceChildren();

    datosDelSistema.hidden = Boolean(enEdicion);
    if (datosDelSistema.hidden) return;

    DEL_SISTEMA.forEach((marcador) => {
      const ficha = boton("acta-ficha acta-ficha--sistema", "bi-magic", marcador.etiqueta);
      ficha.title = "Lo pone el sistema al emitir el acta";
      ficha.addEventListener("click", () => {
        cerrarModal(modalDato);
        insertarDatoEnTexto(marcador.valor);
      });
      lista.appendChild(ficha);
    });
  }

  datoGuardar.addEventListener("click", () => {
    const etiqueta = datoEtiqueta.value.trim();

    if (!etiqueta) {
      datoAviso.textContent = "Escribe cómo se llama el dato.";
      datoAviso.hidden = false;
      datoEtiqueta.focus();
      return;
    }

    const tipo = datoTipo.value;
    const opciones = opcionesTemporales
      .map((x) => x.trim())
      .filter(Boolean)
      .map((x) => ({ valor: x, etiqueta: x }));

    if (infoTipo(tipo).admiteOpciones && opciones.length === 0) {
      datoAviso.textContent = "Escribe al menos una opción para elegir.";
      datoAviso.hidden = false;
      return;
    }

    if (datoEnEdicion) {
      datoEnEdicion.etiqueta = etiqueta;
      datoEnEdicion.tipo = tipo;
      datoEnEdicion.opciones = opciones;
      refrescarFichas(datoEnEdicion.clave);
    } else {
      const sale = !datoOculto.checked;

      const campo = {
        clave: claveLibre(etiqueta),
        etiqueta: etiqueta,
        tipo: tipo,
        requerido: true,
        placeholder: "",
        ayuda: "",
        visibleEnActa: sale,
        opciones: opciones
      };

      estado.campos.push(campo);

      // Lo que no sale en el acta no se inserta: solo se pide al diligenciarla.
      if (sale) {
        insertarDatoEnTexto(campo.clave);
      }
    }

    cerrarModal(modalDato);
    pintarListaDatos();
    pintarEnlaces();
    pintarFirmas();
    refrescarEstado();
  });

  document.getElementById("actaAgregarDato").addEventListener("click", () => abrirDato(null));
  document.querySelectorAll("[data-agregar-dato]").forEach((b) => {
    b.addEventListener("click", () => abrirDato(null));
  });

  // ── Paso 4: la lista de datos ─────────────────────────────────────────────

  const listaDatos = document.getElementById("actaListaDatos");

  function pintarListaDatos() {
    listaDatos.replaceChildren();

    if (estado.campos.length === 0) {
      const vacia = el("div", "espacio-empty");
      vacia.appendChild(icono("bi-input-cursor-text"));
      vacia.appendChild(el("strong", null, "Todavía no hay datos"));
      vacia.appendChild(
        el(
          "span",
          null,
          "Un dato es lo que cambia en cada acta: el nombre de la persona, una fecha, un valor."
        )
      );
      listaDatos.appendChild(vacia);
      return;
    }

    estado.campos.forEach((campo) => {
      const fila = el("div", "acta-dato-fila");

      const marca = el("span", "acta-dato-fila__icono");
      marca.appendChild(icono(infoTipo(campo.tipo).icono));
      fila.appendChild(marca);

      const centro = document.createElement("div");
      centro.appendChild(el("strong", null, campo.etiqueta || "Dato sin nombre"));

      const detalles = el("div", "acta-dato-fila__datos");
      detalles.appendChild(el("span", null, infoTipo(campo.tipo).etiqueta));
      detalles.appendChild(el("span", null, campo.requerido ? "Obligatorio" : "Opcional"));
      detalles.appendChild(
        el("span", null, campo.visibleEnActa ? "Sale en el acta" : "No sale en el acta")
      );

      const enTexto = estado.bloques.some((b) => (b.texto || "").includes("{{" + campo.clave + "}}"));
      if (campo.visibleEnActa && !enTexto) {
        const aviso = el("span", "acta-dato-fila__aviso", "Todavía no está en el texto");
        detalles.appendChild(aviso);
      }

      centro.appendChild(detalles);
      fila.appendChild(centro);

      const acciones = el("div", "acta-dato-fila__acciones");

      if (campo.visibleEnActa && !enTexto) {
        const insertar = boton(
          "espacio-btn espacio-btn--primary espacio-btn--sm",
          "bi-arrow-up-left",
          "Poner en el texto"
        );
        insertar.addEventListener("click", () => {
          insertarDatoEnTexto(campo.clave);
          pintarListaDatos();
          refrescarEstado();
        });
        acciones.appendChild(insertar);
      }

      const editar = boton("espacio-icon-btn", "bi-pencil-fill", null, "Cambiar este dato");
      editar.addEventListener("click", () => abrirDato(campo));
      acciones.appendChild(editar);

      const obligatorio = boton(
        "espacio-icon-btn",
        campo.requerido ? "bi-asterisk" : "bi-dash-lg",
        null,
        campo.requerido ? "Es obligatorio. Tocar para volverlo opcional." : "Es opcional. Tocar para volverlo obligatorio."
      );
      obligatorio.addEventListener("click", () => {
        campo.requerido = !campo.requerido;
        pintarListaDatos();
      });
      acciones.appendChild(obligatorio);

      const visible = boton(
        "espacio-icon-btn",
        campo.visibleEnActa ? "bi-eye-fill" : "bi-eye-slash-fill",
        null,
        campo.visibleEnActa
          ? "Sale en el acta. Tocar para que se pida pero no se imprima."
          : "No sale en el acta. Tocar para poder ponerlo en el texto."
      );
      visible.addEventListener("click", () => {
        campo.visibleEnActa = !campo.visibleEnActa;
        pintarListaDatos();
        refrescarEstado();
      });
      acciones.appendChild(visible);

      const borrar = boton("espacio-icon-btn espacio-icon-btn--danger", "bi-trash3-fill", null, "Borrar el dato");
      borrar.addEventListener("click", () => quitarDato(campo));
      acciones.appendChild(borrar);

      fila.appendChild(acciones);
      listaDatos.appendChild(fila);
    });
  }

  function quitarDato(campo) {
    const enTexto = estado.bloques.some((b) => (b.texto || "").includes("{{" + campo.clave + "}}"));

    const aviso = enTexto
      ? 'El dato "' + (campo.etiqueta || campo.clave) + '" está puesto en el acta. Si lo borras, también se quita del texto. ¿Seguimos?'
      : '¿Borrar el dato "' + (campo.etiqueta || campo.clave) + '"?';

    if (!window.confirm(aviso)) return;

    estado.campos = estado.campos.filter((x) => x !== campo);

    estado.bloques.forEach((bloque) => {
      bloque.texto = (bloque.texto || "").split("{{" + campo.clave + "}}").join("");
      bloque.campos = (bloque.campos || []).filter((clave) => clave !== campo.clave);
    });

    estado.firmas.forEach((firma) => {
      if (firma.campoNombre === campo.clave) firma.campoNombre = "";
      if (firma.campoDocumento === campo.clave) firma.campoDocumento = "";
    });

    ["campoNombre", "campoDocumento", "campoCorreo", "campoUsuario"].forEach((enlace) => {
      if (estado[enlace] === campo.clave) estado[enlace] = "";
    });

    pintarBloques();
    pintarListaDatos();
    pintarFirmas();
    pintarEnlaces();
    refrescarEstado();
  }

  // ── Paso 3: firmas ────────────────────────────────────────────────────────

  const contenedorFirmas = document.getElementById("actaFirmas");

  function pintarFirmas() {
    contenedorFirmas.replaceChildren();

    if (estado.firmas.length === 0) {
      const vacia = el("div", "espacio-empty");
      vacia.appendChild(icono("bi-vector-pen"));
      vacia.appendChild(el("strong", null, "Toda acta necesita al menos una firma"));
      vacia.appendChild(el("span", null, "Normalmente firman dos: quien entrega y quien recibe."));

      const agregar = boton("espacio-btn espacio-btn--primary", "bi-plus-lg", "Agregar la primera firma");
      agregar.addEventListener("click", agregarFirma);
      vacia.appendChild(agregar);

      contenedorFirmas.appendChild(vacia);
      return;
    }

    estado.firmas.forEach((firma, indice) => {
      contenedorFirmas.appendChild(tarjetaFirma(firma, indice));
    });
  }

  function tarjetaFirma(firma, indice) {
    const tarjeta = el("div", "acta-firma-def");

    const cabeza = el("div", "acta-firma-def__cab");
    cabeza.appendChild(el("span", null, "Firma " + (indice + 1)));

    const quitar = boton("espacio-icon-btn espacio-icon-btn--danger", "bi-x-lg", null, "Quitar esta firma");
    quitar.addEventListener("click", () => {
      estado.firmas.splice(indice, 1);
      pintarFirmas();
      refrescarEstado();
    });
    cabeza.appendChild(quitar);
    tarjeta.appendChild(cabeza);

    tarjeta.appendChild(el("div", "acta-firma-def__linea"));

    tarjeta.appendChild(el("label", null, "¿Qué dice debajo de la raya?"));
    const rotulo = document.createElement("input");
    rotulo.type = "text";
    rotulo.value = firma.rotulo;
    rotulo.maxLength = 80;
    rotulo.placeholder = "Ej: Recibe";
    rotulo.addEventListener("input", () => {
      firma.rotulo = rotulo.value;
      refrescarEstado();
    });
    tarjeta.appendChild(rotulo);

    tarjeta.appendChild(el("label", null, "¿Quién firma?"));
    const quien = document.createElement("select");
    [
      ["Emisor", "Yo, con mi firma guardada"],
      ["EnVivo", "La otra persona, en el momento"]
    ].forEach(([valor, texto]) => {
      const opcion = document.createElement("option");
      opcion.value = valor;
      opcion.textContent = texto;
      if (valor === firma.origen) opcion.selected = true;
      quien.appendChild(opcion);
    });
    quien.addEventListener("change", () => {
      firma.origen = quien.value;
      pintarFirmas();
      refrescarEstado();
    });
    tarjeta.appendChild(quien);

    if (firma.origen === "EnVivo") {
      tarjeta.appendChild(el("label", null, "¿Qué nombre va debajo?"));

      const fuente = document.createElement("select");
      const fijo = document.createElement("option");
      fijo.value = "";
      fijo.textContent = "Lo escribo yo aquí";
      fuente.appendChild(fijo);

      estado.campos.forEach((campo) => {
        const opcion = document.createElement("option");
        opcion.value = campo.clave;
        opcion.textContent = "El dato: " + (campo.etiqueta || campo.clave);
        if (campo.clave === firma.campoNombre) opcion.selected = true;
        fuente.appendChild(opcion);
      });

      fuente.addEventListener("change", () => {
        firma.campoNombre = fuente.value;
        pintarFirmas();
        refrescarEstado();
      });
      tarjeta.appendChild(fuente);

      if (!firma.campoNombre) {
        const nombre = document.createElement("input");
        nombre.type = "text";
        nombre.value = firma.nombreFijo;
        nombre.maxLength = 160;
        nombre.placeholder = "Ej: Jefe de Talento Humano";
        nombre.addEventListener("input", () => {
          firma.nombreFijo = nombre.value;
          refrescarEstado();
        });
        tarjeta.appendChild(nombre);
      }
    } else {
      tarjeta.appendChild(el("label", null, "Cargo (opcional)"));
      const cargo = document.createElement("input");
      cargo.type = "text";
      cargo.value = firma.cargoFijo;
      cargo.maxLength = 120;
      cargo.placeholder = "Se toma de tu firma guardada";
      cargo.addEventListener("input", () => {
        firma.cargoFijo = cargo.value;
      });
      tarjeta.appendChild(cargo);
    }

    const obligatoria = el("label", "acta-switch");
    const caja = document.createElement("input");
    caja.type = "checkbox";
    caja.checked = firma.requerida;
    caja.addEventListener("change", () => {
      firma.requerida = caja.checked;
    });
    obligatoria.appendChild(caja);
    obligatoria.appendChild(el("span", null, "Sin esta firma no se puede emitir el acta"));
    tarjeta.appendChild(obligatoria);

    return tarjeta;
  }

  function agregarFirma() {
    const usadas = new Set(estado.firmas.map((x) => x.clave));
    let clave = "firma_" + (estado.firmas.length + 1);
    let sufijo = 2;
    while (usadas.has(clave)) {
      clave = "firma_" + (estado.firmas.length + 1) + "_" + sufijo;
      sufijo += 1;
    }

    const primerTexto = estado.campos.find((c) => c.tipo === "Texto");

    estado.firmas.push({
      clave: clave,
      rotulo: estado.firmas.length === 0 ? "Entrega" : "Recibe",
      origen: estado.firmas.length === 0 ? "Emisor" : "EnVivo",
      campoNombre: estado.firmas.length === 0 ? "" : primerTexto ? primerTexto.clave : "",
      campoDocumento: "",
      nombreFijo: "",
      cargoFijo: "",
      requerida: true
    });

    pintarFirmas();
    refrescarEstado();
  }

  document.getElementById("actaAgregarFirma").addEventListener("click", agregarFirma);

  // ── Identidad y opciones avanzadas ────────────────────────────────────────

  const campoNombrePlantilla = document.getElementById("actaNombre");
  const campoDescripcion = document.getElementById("actaDescripcion");
  const campoIcono = document.getElementById("actaIcono");
  const campoTitulo = document.getElementById("actaTitulo");
  const campoNumerar = document.getElementById("actaNumerar");

  const autoCrecer = (textarea) => {
    textarea.style.height = "auto";
    textarea.style.height = textarea.scrollHeight + "px";
  };

  campoNombrePlantilla.addEventListener("input", () => {
    estado.nombre = campoNombrePlantilla.value;
    refrescarEstado();
  });

  campoDescripcion.addEventListener("input", () => {
    estado.descripcion = campoDescripcion.value;
  });

  campoIcono.addEventListener("change", () => {
    estado.icono = campoIcono.value;
  });

  campoTitulo.addEventListener("input", () => {
    estado.tituloActa = campoTitulo.value;
    autoCrecer(campoTitulo);
    refrescarEstado();
  });

  campoNumerar.addEventListener("change", () => {
    estado.numerarTitulos = campoNumerar.checked;
  });

  const ENLACES = [
    ["actaCampoNombre", "campoNombre", "Automático (el primer dato de texto)"],
    ["actaCampoCorreo", "campoCorreo", "No enviar copia por correo"]
  ];

  function pintarEnlaces() {
    ENLACES.forEach(([id, propiedad, textoVacio]) => {
      const select = document.getElementById(id);
      if (!select) return;

      select.replaceChildren();

      const vacio2 = document.createElement("option");
      vacio2.value = "";
      vacio2.textContent = textoVacio;
      select.appendChild(vacio2);

      estado.campos.forEach((campo) => {
        if (propiedad === "campoCorreo" && campo.tipo !== "Correo") return;

        const opcion = document.createElement("option");
        opcion.value = campo.clave;
        opcion.textContent = campo.etiqueta || campo.clave;
        if (campo.clave === estado[propiedad]) opcion.selected = true;
        select.appendChild(opcion);
      });

      if (!select.dataset.enlazado) {
        select.dataset.enlazado = "si";
        select.addEventListener("change", () => {
          estado[propiedad] = select.value;
        });
      }
    });
  }

  function pintarIdentidad() {
    campoNombrePlantilla.value = estado.nombre;
    campoDescripcion.value = estado.descripcion;
    campoIcono.value = estado.icono;
    campoTitulo.value = estado.tituloActa;
    campoNumerar.checked = estado.numerarTitulos;
    autoCrecer(campoTitulo);
  }

  // ── Qué falta ─────────────────────────────────────────────────────────────

  const barraEstado = document.getElementById("actaEstado");
  const panelPendientes = document.getElementById("actaPendientes");

  function pendientes() {
    const lista = [];

    if (!estado.nombre.trim()) {
      lista.push({ texto: "Ponle un nombre al acta", ancla: "actaNombre" });
    }

    if (!estado.tituloActa.trim()) {
      lista.push({ texto: "Escribe el título que va en el documento", ancla: "actaTitulo" });
    }

    const conTexto = estado.bloques.filter((b) =>
      b.tipo === "Datos" ? (b.campos || []).length > 0 : b.tipo !== "Separador" && (b.texto || "").trim() !== ""
    );
    if (conTexto.length === 0) {
      lista.push({ texto: "Escribe el texto del acta", ancla: "paso2" });
    }

    if (estado.campos.length === 0) {
      lista.push({ texto: "Agrega al menos un dato que se llene al emitirla", ancla: "paso4" });
    } else if (!estado.campos.some((c) => c.tipo === "Texto")) {
      lista.push({
        texto: "Agrega un dato de tipo Texto con el nombre de la persona",
        ancla: "paso4"
      });
    }

    const sinNombre = estado.campos.find((c) => !c.etiqueta.trim());
    if (sinNombre) {
      lista.push({ texto: "Hay un dato sin nombre", ancla: "paso4" });
    }

    if (estado.firmas.length === 0) {
      lista.push({ texto: "Agrega al menos una firma", ancla: "paso3" });
    } else {
      if (estado.firmas.every((f) => f.origen === "Emisor")) {
        lista.push({ texto: "Agrega una firma para la otra persona", ancla: "paso3" });
      }

      if (estado.firmas.some((f) => !f.rotulo.trim())) {
        lista.push({ texto: "Hay una firma sin rótulo", ancla: "paso3" });
      }

      const huerfana = estado.firmas.find(
        (f) => f.origen === "EnVivo" && !f.campoNombre && !f.nombreFijo.trim()
      );
      if (huerfana) {
        lista.push({
          texto: 'Falta el nombre de la firma "' + (huerfana.rotulo || "sin rótulo") + '"',
          ancla: "paso3"
        });
      }
    }

    const claves = new Set(estado.campos.map((c) => c.clave));
    const rotos = estado.bloques.some((b) =>
      [...(b.texto || "").matchAll(/\{\{([a-zA-Z0-9_]+)\}\}/g)].some(
        (m) => !m[1].startsWith("__") && !claves.has(m[1])
      )
    );
    if (rotos) {
      lista.push({ texto: "En el texto quedó un dato que ya no existe", ancla: "paso2" });
    }

    return lista;
  }

  function refrescarEstado() {
    if (arrancado) hayCambios = true;

    const falta = pendientes();
    const textoEstado = barraEstado.querySelector("[data-texto]");
    const marca = barraEstado.querySelector(".bi");

    barraEstado.classList.toggle("is-listo", falta.length === 0);
    barraEstado.classList.toggle("is-pendiente", falta.length > 0);

    if (falta.length === 0) {
      textoEstado.textContent = publicada ? "Publicada y al día" : "Lista para publicar";
      marca.className = "bi bi-check-circle-fill";
    } else {
      textoEstado.textContent =
        falta.length === 1 ? "Falta 1 cosa" : "Faltan " + falta.length + " cosas";
      marca.className = "bi bi-exclamation-circle-fill";
    }

    const lista = panelPendientes.querySelector("[data-lista]");
    lista.replaceChildren();

    falta.forEach((item) => {
      const fila = document.createElement("li");
      const enlace = boton(null, "bi-arrow-right-short", item.texto);
      enlace.addEventListener("click", () => irA(item.ancla));
      fila.appendChild(enlace);
      lista.appendChild(fila);
    });

    if (falta.length === 0) {
      panelPendientes.hidden = true;
      barraEstado.setAttribute("aria-expanded", "false");
    }

    marcarError("actaNombre", falta.some((x) => x.ancla === "actaNombre") ? "Falta el nombre" : "");
    marcarError("actaTitulo", falta.some((x) => x.ancla === "actaTitulo") ? "Falta el título" : "");
  }

  function marcarError(id, mensaje) {
    const destino = document.querySelector('[data-error-de="' + id + '"]');
    const campo = document.getElementById(id);
    if (!destino || !campo) return;

    destino.textContent = mensaje;
    destino.classList.toggle("acta-error", Boolean(mensaje));
    campo.classList.toggle("is-error", Boolean(mensaje));
  }

  function irA(ancla) {
    const destino = document.getElementById(ancla);
    if (!destino) return;

    destino.scrollIntoView({ behavior: "smooth", block: "center" });

    if (destino.tagName === "INPUT" || destino.tagName === "TEXTAREA") {
      window.setTimeout(() => destino.focus(), 320);
    } else {
      destino.classList.add("is-resaltado");
      window.setTimeout(() => destino.classList.remove("is-resaltado"), 1600);
    }

    panelPendientes.hidden = true;
    barraEstado.setAttribute("aria-expanded", "false");
  }

  barraEstado.addEventListener("click", () => {
    if (pendientes().length === 0) return;
    const abierto = panelPendientes.hidden;
    panelPendientes.hidden = !abierto;
    barraEstado.setAttribute("aria-expanded", String(abierto));
  });

  function avisarEnBarra(mensaje) {
    const lista = panelPendientes.querySelector("[data-lista]");
    lista.replaceChildren();
    const fila = document.createElement("li");
    fila.textContent = mensaje;
    lista.appendChild(fila);
    panelPendientes.hidden = false;
    window.setTimeout(refrescarEstado, 3500);
  }

  // ── Servidor ──────────────────────────────────────────────────────────────

  const obtenerToken = () => {
    const entrada = document.querySelector('input[name="__RequestVerificationToken"]');
    return entrada ? entrada.value : "";
  };

  const enviar = (url, cuerpo) =>
    fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
        RequestVerificationToken: obtenerToken()
      },
      body: JSON.stringify(cuerpo)
    });

  const modalVista = document.getElementById("actaModalVista");
  const vistaErrores = document.getElementById("actaVistaErrores");
  const vistaHoja = document.getElementById("actaVistaHoja");

  document.getElementById("actaVerComoQueda").addEventListener("click", async () => {
    abrirModal(modalVista);
    vistaErrores.hidden = true;
    vistaHoja.hidden = false;
    document.getElementById("actaVistaCuerpo").replaceChildren(el("p", null, "Armando el documento…"));
    document.getElementById("actaVistaFirmas").replaceChildren();

    try {
      const respuesta = await enviar("/EspacioCorporativo/ActaPlantillaPrevisualizar", estado);
      const datos = await respuesta.json();

      if (!respuesta.ok) {
        mostrarErroresDeVista(datos.errores || ["No se pudo armar la vista."]);
        return;
      }

      document.getElementById("actaVistaTitulo").textContent = datos.tituloActa;
      document.getElementById("actaVistaFecha").textContent = datos.fecha;

      // El cuerpo llega renderizado y codificado por el servidor.
      document.getElementById("actaVistaCuerpo").innerHTML = datos.cuerpoHtml;

      const firmas = document.getElementById("actaVistaFirmas");
      (datos.firmas || []).forEach((firma) => {
        const bloque = el("div", "espacio-acta-firma");

        const marco = el("div", "espacio-acta-firma__imagen");
        const hueco = el("span", "acta-firma-hueco", firma.esEmisor ? "Tu firma guardada" : "Firma al emitir");
        marco.appendChild(hueco);
        bloque.appendChild(marco);

        bloque.appendChild(el("div", "espacio-acta-firma__linea"));
        bloque.appendChild(el("strong", null, firma.nombre));
        if (firma.cargo) bloque.appendChild(el("span", null, firma.cargo));
        if (firma.documento) bloque.appendChild(el("span", null, "C.C. " + firma.documento));
        bloque.appendChild(el("span", null, firma.rotulo));

        firmas.appendChild(bloque);
      });
    } catch (error) {
      console.error(error);
      mostrarErroresDeVista(["No se pudo conectar con el servidor."]);
    }
  });

  function mostrarErroresDeVista(errores) {
    const lista = vistaErrores.querySelector("[data-lista]");
    lista.replaceChildren();

    errores.forEach((error) => {
      const fila = document.createElement("li");
      fila.textContent = error;
      lista.appendChild(fila);
    });

    vistaErrores.hidden = false;
    vistaHoja.hidden = true;
  }

  async function guardar(publicar) {
    const botones = [
      document.getElementById("actaGuardarBorrador"),
      document.getElementById("actaPublicar")
    ];
    botones.forEach((b) => (b.disabled = true));

    try {
      const respuesta = await enviar(
        "/EspacioCorporativo/ActaPlantillaGuardar?publicar=" + (publicar ? "true" : "false"),
        estado
      );
      const datos = await respuesta.json();

      if (!respuesta.ok) {
        mostrarErroresDeVista(datos.errores || ["No se pudo guardar."]);
        abrirModal(modalVista);
        return;
      }

      if (!estado.id && datos.id) {
        estado.id = datos.id;
        window.history.replaceState({}, "", "/EspacioCorporativo/ActaPlantillaEditar/" + datos.id);
      }

      publicada = datos.publicada === true;
      confirmar(datos.mensaje);
      refrescarEstado();
      hayCambios = false;
    } catch (error) {
      console.error(error);
      mostrarErroresDeVista(["No se pudo conectar con el servidor."]);
      abrirModal(modalVista);
    } finally {
      botones.forEach((b) => (b.disabled = false));
    }
  }

  function confirmar(mensaje) {
    const aviso = el("div", "acta-confirmacion");
    aviso.appendChild(icono("bi-check-circle-fill"));
    aviso.appendChild(el("span", null, mensaje));
    document.body.appendChild(aviso);

    window.setTimeout(() => aviso.classList.add("is-visible"), 20);
    window.setTimeout(() => {
      aviso.classList.remove("is-visible");
      window.setTimeout(() => aviso.remove(), 300);
    }, 4200);
  }

  document.getElementById("actaGuardarBorrador").addEventListener("click", () => guardar(false));
  document.getElementById("actaPublicar").addEventListener("click", () => guardar(true));

  // ── Modelos de arranque ───────────────────────────────────────────────────

  const modalModelos = document.getElementById("actaModalModelos");
  const contenedorModelos = document.getElementById("actaModelos");

  function pintarModelos() {
    contenedorModelos.replaceChildren();

    MODELOS.forEach((modelo) => {
      const tarjeta = boton("acta-modelo", null, null);
      tarjeta.replaceChildren();

      const marca = el("span", "acta-modelo__icono");
      marca.appendChild(icono(modelo.icono));
      tarjeta.appendChild(marca);

      const textos = document.createElement("div");
      textos.appendChild(el("strong", null, modelo.nombre));
      textos.appendChild(el("span", null, modelo.descripcion));
      tarjeta.appendChild(textos);

      tarjeta.appendChild(icono("bi-arrow-right"));

      tarjeta.addEventListener("click", () => {
        const base = normalizarDefinicion(modelo.definicion);
        base.id = estado.id;
        base.nombre = estado.nombre;
        base.descripcion = estado.descripcion;
        estado = base;

        cerrarModal(modalModelos);
        pintarTodo();
        window.setTimeout(() => campoNombrePlantilla.focus(), 200);
      });

      contenedorModelos.appendChild(tarjeta);
    });
  }

  // ── Arranque ──────────────────────────────────────────────────────────────

  function pintarTodo() {
    pintarIdentidad();
    pintarBloques();
    pintarListaDatos();
    pintarFirmas();
    pintarEnlaces();
    refrescarEstado();
  }

  pintarModelos();

  try {
    pintarTodo();
  } catch (error) {
    console.error(error);
    avisarEnBarra("Algo falló al abrir el editor. Recarga la página; lo guardado no se pierde.");
  }

  arrancado = true;

  if (CONFIG.elegirModelo) {
    abrirModal(modalModelos);
  }

  // Nadie debería perder un acta a medio escribir por cerrar la pestaña.
  window.addEventListener("beforeunload", (evento) => {
    if (!hayCambios) return;
    evento.preventDefault();
    evento.returnValue = "";
  });
})();
