// ════════════════════════════════════════════════════════════════════════════
// Mi espacio corporativo — interacciones de la pantalla principal y las
// pantallas de administración (activos y documentación).
// ════════════════════════════════════════════════════════════════════════════
(() => {
  "use strict";

  // ── Modales ───────────────────────────────────────────────────────────────

  const abrirModal = (modal) => {
    if (!modal) return;
    modal.classList.add("is-open");
    document.body.style.overflow = "hidden";
    const primerCampo = modal.querySelector("input:not([type=hidden]), select, textarea");
    if (primerCampo) {
      window.setTimeout(() => primerCampo.focus(), 60);
    }
  };

  const cerrarModal = (modal) => {
    if (!modal) return;
    modal.classList.remove("is-open");
    if (!document.querySelector(".espacio-modal.is-open")) {
      document.body.style.overflow = "";
    }
  };

  document.querySelectorAll("[data-modal-cerrar]").forEach((boton) => {
    boton.addEventListener("click", () => cerrarModal(boton.closest(".espacio-modal")));
  });

  document.querySelectorAll(".espacio-modal").forEach((modal) => {
    modal.addEventListener("mousedown", (event) => {
      if (event.target === modal) {
        cerrarModal(modal);
      }
    });
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      document.querySelectorAll(".espacio-modal.is-open").forEach(cerrarModal);
    }
  });

  const obtenerToken = () => {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : "";
  };

  // ── Reporte de novedad ────────────────────────────────────────────────────

  const novedadModal = document.getElementById("espacioNovedadModal");
  const novedadSelect = document.getElementById("novedadActivoId");
  const equipoLibreWrap = document.getElementById("novedadEquipoLibreWrap");
  const equipoLibreInput = document.getElementById("novedadEquipoLibre");

  const sincronizarEquipoLibre = () => {
    if (!novedadSelect || !equipoLibreWrap) return;
    const esLibre = novedadSelect.value === "";
    equipoLibreWrap.style.display = esLibre ? "" : "none";
    if (equipoLibreInput) {
      equipoLibreInput.required = esLibre;
      if (!esLibre) {
        equipoLibreInput.value = "";
      }
    }
  };

  if (novedadSelect) {
    novedadSelect.addEventListener("change", sincronizarEquipoLibre);
    sincronizarEquipoLibre();
  }

  document.querySelectorAll("[data-novedad-abrir]").forEach((boton) => {
    boton.addEventListener("click", () => {
      const activoId = boton.getAttribute("data-activo-id") || "";
      if (novedadSelect && activoId) {
        novedadSelect.value = activoId;
      }
      sincronizarEquipoLibre();
      abrirModal(novedadModal);
    });
  });

  // ── Favoritos ─────────────────────────────────────────────────────────────

  document.querySelectorAll("[data-favorito-btn]").forEach((boton) => {
    boton.addEventListener("click", async () => {
      const documentoId = boton.getAttribute("data-favorito-btn");
      boton.disabled = true;

      try {
        const respuesta = await fetch(`/EspacioCorporativo/AlternarFavorito?id=${encodeURIComponent(documentoId)}`, {
          method: "POST",
          headers: {
            RequestVerificationToken: obtenerToken(),
            Accept: "application/json"
          }
        });

        if (!respuesta.ok) {
          throw new Error("No se pudo actualizar el favorito.");
        }

        const datos = await respuesta.json();
        const tarjeta = boton.closest(".espacio-doc-card");

        boton.classList.toggle("is-active", datos.esFavorito);
        boton.title = datos.esFavorito ? "Quitar de favoritos" : "Agregar a favoritos";
        if (tarjeta) {
          tarjeta.dataset.favorito = datos.esFavorito ? "true" : "false";
        }
        aplicarFiltrosDocumentos();
      } catch (error) {
        console.error(error);
        window.alert("No fue posible actualizar el favorito. Intenta nuevamente.");
      } finally {
        boton.disabled = false;
      }
    });
  });

  // ── Buscador y filtros de documentación ───────────────────────────────────

  const docsGrid = document.getElementById("espacioDocsGrid");
  const docBuscar = document.getElementById("espacioDocBuscar");
  const docTipo = document.getElementById("espacioDocTipo");
  const docFavoritosBtn = document.getElementById("espacioDocFavoritos");
  const docsVacio = document.getElementById("espacioDocsVacio");
  const docsContador = document.getElementById("espacioDocsContador");
  const categoriaChips = document.querySelectorAll("#espacioDocCategorias .espacio-cat-chip");

  let categoriaActiva = "";
  let soloFavoritos = false;

  const normalizar = (valor) =>
    (valor || "")
      .toString()
      .toLowerCase()
      .normalize("NFD")
      .replace(/[̀-ͯ]/g, "");

  function aplicarFiltrosDocumentos() {
    if (!docsGrid) return;

    const termino = normalizar(docBuscar ? docBuscar.value.trim() : "");
    const tipo = docTipo ? docTipo.value : "";
    let visibles = 0;

    docsGrid.querySelectorAll(".espacio-doc-card").forEach((tarjeta) => {
      const coincideTermino = termino === "" || normalizar(tarjeta.dataset.busqueda).includes(termino);
      const coincideCategoria = categoriaActiva === "" || tarjeta.dataset.categoria === categoriaActiva;
      const coincideTipo = tipo === "" || tarjeta.dataset.tipo === tipo;
      const coincideFavorito = !soloFavoritos || tarjeta.dataset.favorito === "true";
      const visible = coincideTermino && coincideCategoria && coincideTipo && coincideFavorito;

      tarjeta.style.display = visible ? "" : "none";
      if (visible) visibles += 1;
    });

    if (docsVacio) {
      docsVacio.style.display = visibles === 0 ? "" : "none";
    }
    if (docsContador) {
      docsContador.textContent = `${visibles} documento(s)`;
    }
  }

  if (docBuscar) {
    docBuscar.addEventListener("input", aplicarFiltrosDocumentos);
  }

  if (docTipo) {
    docTipo.addEventListener("change", aplicarFiltrosDocumentos);
  }

  if (docFavoritosBtn) {
    docFavoritosBtn.addEventListener("click", () => {
      soloFavoritos = !soloFavoritos;
      docFavoritosBtn.classList.toggle("espacio-btn--primary", soloFavoritos);
      docFavoritosBtn.classList.toggle("espacio-btn--ghost", !soloFavoritos);
      docFavoritosBtn.setAttribute("aria-pressed", soloFavoritos ? "true" : "false");
      aplicarFiltrosDocumentos();
    });
  }

  categoriaChips.forEach((chip) => {
    chip.addEventListener("click", () => {
      categoriaChips.forEach((otro) => otro.classList.remove("is-active"));
      chip.classList.add("is-active");
      categoriaActiva = chip.dataset.categoria || "";
      aplicarFiltrosDocumentos();
    });
  });

  // ── Lector de documentos escritos ─────────────────────────────────────────

  const lectorModal = document.getElementById("espacioLectorModal");
  const lectorTitulo = document.getElementById("espacioLectorTitulo");
  const lectorSubtitulo = document.getElementById("espacioLectorSubtitulo");
  const lectorContenido = document.getElementById("espacioLectorContenido");

  document.querySelectorAll("[data-doc-leer]").forEach((boton) => {
    boton.addEventListener("click", async () => {
      const documentoId = boton.getAttribute("data-doc-leer");

      try {
        const respuesta = await fetch(`/EspacioCorporativo/DocumentoTexto?id=${encodeURIComponent(documentoId)}`, {
          headers: { Accept: "application/json" }
        });

        if (!respuesta.ok) {
          throw new Error("No se pudo cargar el documento.");
        }

        const datos = await respuesta.json();
        const detalles = [datos.categoria, datos.tipoDocumento, datos.version ? `v${datos.version}` : null, datos.publicado]
          .filter(Boolean)
          .join(" · ");

        if (lectorTitulo) lectorTitulo.textContent = datos.titulo;
        if (lectorSubtitulo) lectorSubtitulo.textContent = detalles;
        if (lectorContenido) lectorContenido.textContent = datos.contenido || "Este documento no tiene contenido.";
        abrirModal(lectorModal);
      } catch (error) {
        console.error(error);
        window.alert("No fue posible abrir el documento.");
      }
    });
  });

  // ── Administración de activos ─────────────────────────────────────────────

  const activoModal = document.getElementById("espacioActivoModal");
  const activoForm = document.getElementById("espacioActivoForm");
  const activoModalTitulo = document.getElementById("espacioActivoModalTitulo");

  const limpiarFormularioActivo = () => {
    if (!activoForm) return;
    activoForm.reset();
    const idInput = activoForm.querySelector('[name="Id"]');
    if (idInput) idInput.value = "";
  };

  document.querySelectorAll("[data-activo-nuevo]").forEach((boton) => {
    boton.addEventListener("click", () => {
      limpiarFormularioActivo();
      if (activoModalTitulo) activoModalTitulo.textContent = "Nuevo activo";
      abrirModal(activoModal);
    });
  });

  document.querySelectorAll("[data-activo-editar]").forEach((boton) => {
    boton.addEventListener("click", () => {
      if (!activoForm) return;
      limpiarFormularioActivo();

      const campos = {
        Id: boton.dataset.id,
        TipoActivo: boton.dataset.tipoActivo,
        NombreEquipo: boton.dataset.nombreEquipo,
        Marca: boton.dataset.marca,
        Serie: boton.dataset.serie,
        Serial: boton.dataset.serial,
        Especificaciones: boton.dataset.especificaciones,
        CodigoActivo: boton.dataset.codigoActivo,
        ResponsableUserId: boton.dataset.responsableId,
        Estado: boton.dataset.estado,
        Nota: boton.dataset.nota
      };

      Object.entries(campos).forEach(([nombre, valor]) => {
        const campo = activoForm.querySelector(`[name="${nombre}"]`);
        if (campo) campo.value = valor || "";
      });

      if (activoModalTitulo) activoModalTitulo.textContent = `Editar activo #${boton.dataset.id}`;
      abrirModal(activoModal);
    });
  });

  // Historial del activo
  const historialModal = document.getElementById("espacioHistorialModal");
  const historialContenido = document.getElementById("espacioHistorialContenido");
  const historialTitulo = document.getElementById("espacioHistorialTitulo");

  document.querySelectorAll("[data-activo-historial]").forEach((boton) => {
    boton.addEventListener("click", async () => {
      const activoId = boton.getAttribute("data-activo-historial");
      if (historialTitulo) historialTitulo.textContent = `Historial del activo #${activoId}`;
      if (historialContenido) historialContenido.innerHTML = "<p>Cargando...</p>";
      abrirModal(historialModal);

      try {
        const respuesta = await fetch(`/EspacioCorporativo/HistorialActivo?id=${encodeURIComponent(activoId)}`, {
          headers: { Accept: "application/json" }
        });

        if (!respuesta.ok) {
          throw new Error("No se pudo cargar el historial.");
        }

        const datos = await respuesta.json();
        if (!historialContenido) return;

        if (!datos.movimientos || datos.movimientos.length === 0) {
          historialContenido.innerHTML = "<p>Este activo aún no registra movimientos.</p>";
          return;
        }

        const items = datos.movimientos
          .map((movimiento) => {
            const item = document.createElement("div");
            item.className = "espacio-timeline__item";

            const titulo = document.createElement("strong");
            titulo.textContent = `${movimiento.tipo} — ${movimiento.detalle}`;

            const meta = document.createElement("span");
            meta.textContent = `${movimiento.fecha}${movimiento.usuario ? ` · ${movimiento.usuario}` : ""}`;

            item.appendChild(titulo);
            item.appendChild(meta);
            return item.outerHTML;
          })
          .join("");

        historialContenido.innerHTML = `<div class="espacio-timeline">${items}</div>`;
      } catch (error) {
        console.error(error);
        if (historialContenido) {
          historialContenido.innerHTML = "<p>No fue posible cargar el historial.</p>";
        }
      }
    });
  });

  // Gestión de novedades
  const novedadGestionModal = document.getElementById("espacioNovedadGestionModal");
  const novedadGestionForm = document.getElementById("espacioNovedadGestionForm");

  // Orden del flujo; "Rechazada" es una salida lateral, no un paso más.
  const FLUJO = ["Reportada", "En proceso", "Resuelta"];

  function pintarFlujo(estadoActual) {
    const flujo = document.getElementById("espacioNovedadFlujo");
    if (!flujo) return;

    const rechazada = estadoActual === "Rechazada";
    const indiceActual = FLUJO.indexOf(estadoActual);

    flujo.querySelectorAll("[data-paso]").forEach((paso) => {
      const esAlterno = paso.hasAttribute("data-paso-alterno");
      paso.classList.remove("is-hecho", "is-actual", "is-rechazado");

      // "Rechazada" es una salida lateral: ocupa el lugar de "Resuelta", no se suma a ella.
      if (esAlterno) {
        paso.hidden = !rechazada;
        if (rechazada) {
          paso.classList.add("is-rechazado");
        }
        return;
      }

      const indice = FLUJO.indexOf(paso.dataset.paso);

      if (rechazada) {
        paso.hidden = paso.dataset.paso === "Resuelta";
        if (indice === 0) {
          paso.classList.add("is-hecho");
        }
        return;
      }

      paso.hidden = false;

      if (indice < indiceActual) {
        paso.classList.add("is-hecho");
      } else if (indice === indiceActual) {
        paso.classList.add("is-actual");
      }
    });
  }

  function mostrarAccionesFlujo(estadoActual) {
    if (!novedadGestionForm) return;
    novedadGestionForm.querySelectorAll("[data-flujo-desde]").forEach((grupo) => {
      grupo.hidden = grupo.dataset.flujoDesde !== estadoActual;
    });
  }

  document.querySelectorAll("[data-novedad-gestionar]").forEach((boton) => {
    boton.addEventListener("click", () => {
      if (!novedadGestionForm) return;

      const campos = {
        Id: boton.dataset.id,
        Clasificacion: boton.dataset.clasificacion,
        Prioridad: boton.dataset.prioridad,
        RespuestaAdmin: boton.dataset.respuesta
      };

      Object.entries(campos).forEach(([nombre, valor]) => {
        const campo = novedadGestionForm.querySelector(`[name="${nombre}"]`);
        if (campo) campo.value = valor || "";
      });

      const estadoActual = boton.dataset.estado || "Reportada";
      pintarFlujo(estadoActual);
      mostrarAccionesFlujo(estadoActual);

      const resumen = document.getElementById("espacioNovedadResumen");
      if (resumen) {
        resumen.textContent = `#${boton.dataset.id} · ${boton.dataset.tipo || ""} · ${boton.dataset.equipo || ""}`;
      }

      const detalle = document.getElementById("espacioNovedadDetalle");
      if (detalle) {
        detalle.textContent = boton.dataset.descripcion || "";
      }

      abrirModal(novedadGestionModal);
    });
  });

  // ── Administración de documentación ───────────────────────────────────────

  const documentoModal = document.getElementById("espacioDocumentoModal");
  const documentoForm = document.getElementById("espacioDocumentoForm");
  const documentoModalTitulo = document.getElementById("espacioDocumentoModalTitulo");
  const tipoContenidoSelect = documentoForm ? documentoForm.querySelector('[name="TipoContenido"]') : null;

  const sincronizarTipoContenido = () => {
    if (!documentoForm || !tipoContenidoSelect) return;
    const valor = tipoContenidoSelect.value;

    documentoForm.querySelectorAll("[data-contenido]").forEach((bloque) => {
      const aplica = bloque.dataset.contenido === valor;
      bloque.style.display = aplica ? "" : "none";
      bloque.querySelectorAll("input, textarea").forEach((campo) => {
        campo.disabled = !aplica;
      });
    });
  };

  if (tipoContenidoSelect) {
    tipoContenidoSelect.addEventListener("change", sincronizarTipoContenido);
  }

  const limpiarFormularioDocumento = () => {
    if (!documentoForm) return;
    documentoForm.reset();
    const idInput = documentoForm.querySelector('[name="Id"]');
    if (idInput) idInput.value = "";
    const archivoActual = document.getElementById("espacioDocArchivoActual");
    if (archivoActual) archivoActual.textContent = "";
    sincronizarTipoContenido();
  };

  document.querySelectorAll("[data-documento-nuevo]").forEach((boton) => {
    boton.addEventListener("click", () => {
      limpiarFormularioDocumento();
      if (documentoModalTitulo) documentoModalTitulo.textContent = "Nuevo documento";
      abrirModal(documentoModal);
    });
  });

  document.querySelectorAll("[data-documento-editar]").forEach((boton) => {
    boton.addEventListener("click", () => {
      if (!documentoForm) return;
      limpiarFormularioDocumento();

      const campos = {
        Id: boton.dataset.id,
        Titulo: boton.dataset.titulo,
        Descripcion: boton.dataset.descripcion,
        Categoria: boton.dataset.categoria,
        TipoDocumento: boton.dataset.tipoDocumento,
        TipoContenido: boton.dataset.tipoContenido,
        Version: boton.dataset.version,
        CodigoDocumento: boton.dataset.codigo,
        Etiquetas: boton.dataset.etiquetas,
        EnlaceUrl: boton.dataset.enlace,
        ContenidoTexto: boton.dataset.contenido,
        FechaVigencia: boton.dataset.vigencia
      };

      Object.entries(campos).forEach(([nombre, valor]) => {
        const campo = documentoForm.querySelector(`[name="${nombre}"]`);
        if (campo) campo.value = valor || "";
      });

      const publicado = documentoForm.querySelector('[name="Publicado"]');
      if (publicado) publicado.checked = boton.dataset.publicado === "true";

      const destacado = documentoForm.querySelector('[name="Destacado"]');
      if (destacado) destacado.checked = boton.dataset.destacado === "true";

      const archivoActual = document.getElementById("espacioDocArchivoActual");
      if (archivoActual && boton.dataset.archivo) {
        archivoActual.textContent = `Archivo actual: ${boton.dataset.archivo}. Deja el campo vacio para conservarlo.`;
      }

      sincronizarTipoContenido();
      if (documentoModalTitulo) documentoModalTitulo.textContent = `Editar documento #${boton.dataset.id}`;
      abrirModal(documentoModal);
    });
  });

  sincronizarTipoContenido();

  // ── Confirmaciones de borrado ─────────────────────────────────────────────

  document.querySelectorAll("[data-confirmar]").forEach((formulario) => {
    formulario.addEventListener("submit", (event) => {
      const mensaje = formulario.getAttribute("data-confirmar");
      if (!window.confirm(mensaje)) {
        event.preventDefault();
      }
    });
  });

  // ── Filtro rápido en tablas de administración ─────────────────────────────

  document.querySelectorAll("[data-tabla-filtro]").forEach((input) => {
    const tablaId = input.getAttribute("data-tabla-filtro");
    const tabla = document.getElementById(tablaId);
    if (!tabla) return;

    input.addEventListener("input", () => {
      const termino = normalizar(input.value.trim());
      tabla.querySelectorAll("tbody tr").forEach((fila) => {
        const texto = normalizar(fila.textContent);
        fila.style.display = termino === "" || texto.includes(termino) ? "" : "none";
      });
    });
  });

  // ══════════════════════════════════════════════════════════════════════════
  // Firma digital: pad de trazo y actas de entrega / devolución
  // ══════════════════════════════════════════════════════════════════════════

  /**
   * Convierte un <canvas> en un pad de firma. Guarda el trazo como PNG data URL.
   * Se redimensiona según el devicePixelRatio para que la firma no salga pixelada.
   */
  function crearPadFirma(canvas) {
    if (!canvas) return null;

    const contexto = canvas.getContext("2d");
    let dibujando = false;
    let hayTrazo = false;

    const pintarFondo = (ancho, alto) => {
      contexto.fillStyle = "#ffffff";
      contexto.fillRect(0, 0, ancho, alto);
      contexto.lineWidth = 2.2;
      contexto.lineCap = "round";
      contexto.lineJoin = "round";
      contexto.strokeStyle = "#16202e";
    };

    const ajustar = () => {
      const rect = canvas.getBoundingClientRect();
      if (rect.width === 0) return;
      const previo = hayTrazo ? canvas.toDataURL("image/png") : "";
      const ratio = Math.max(window.devicePixelRatio || 1, 1);
      canvas.width = Math.round(rect.width * ratio);
      canvas.height = Math.round(rect.height * ratio);
      contexto.setTransform(ratio, 0, 0, ratio, 0, 0);
      pintarFondo(rect.width, rect.height);
      if (previo) {
        pintar(previo);
      }
    };

    const pintar = (dataUrl) => {
      if (!dataUrl) return;
      const imagen = new Image();
      imagen.onload = () => {
        const rect = canvas.getBoundingClientRect();
        contexto.drawImage(imagen, 0, 0, rect.width, rect.height);
        hayTrazo = true;
      };
      imagen.src = dataUrl;
    };

    const punto = (evento) => {
      const rect = canvas.getBoundingClientRect();
      return { x: evento.clientX - rect.left, y: evento.clientY - rect.top };
    };

    canvas.addEventListener("pointerdown", (evento) => {
      evento.preventDefault();
      canvas.setPointerCapture(evento.pointerId);
      const p = punto(evento);
      contexto.beginPath();
      contexto.moveTo(p.x, p.y);
      dibujando = true;
      hayTrazo = true;
    });

    canvas.addEventListener("pointermove", (evento) => {
      if (!dibujando) return;
      evento.preventDefault();
      const p = punto(evento);
      contexto.lineTo(p.x, p.y);
      contexto.stroke();
    });

    ["pointerup", "pointercancel", "pointerleave"].forEach((tipo) => {
      canvas.addEventListener(tipo, () => {
        dibujando = false;
      });
    });

    return {
      ajustar,
      cargar: (dataUrl) => {
        ajustar();
        pintar(dataUrl);
      },
      limpiar: () => {
        const rect = canvas.getBoundingClientRect();
        contexto.clearRect(0, 0, rect.width, rect.height);
        pintarFondo(rect.width, rect.height);
        hayTrazo = false;
      },
      tieneTrazo: () => hayTrazo,
      obtener: () => (hayTrazo ? canvas.toDataURL("image/png") : "")
    };
  }

  const pads = new Map();
  document.querySelectorAll("canvas[id$='Canvas']").forEach((canvas) => {
    const pad = crearPadFirma(canvas);
    if (pad) pads.set(canvas.id, pad);
  });

  document.querySelectorAll("[data-firma-limpiar]").forEach((boton) => {
    boton.addEventListener("click", () => {
      pads.get(boton.dataset.firmaLimpiar)?.limpiar();
    });
  });

  window.addEventListener("resize", () => {
    document.querySelectorAll(".espacio-modal.is-open canvas[id$='Canvas']").forEach((canvas) => {
      pads.get(canvas.id)?.ajustar();
    });
  });

  const mostrarAviso = (elemento, mensaje, esError) => {
    if (!elemento) return;
    elemento.textContent = mensaje || "";
    elemento.hidden = !mensaje;
    elemento.classList.toggle("is-error", Boolean(esError));
  };

  // ── Mi firma guardada ─────────────────────────────────────────────────────

  const miFirmaModal = document.getElementById("espacioMiFirmaModal");
  const miFirmaForm = document.getElementById("espacioMiFirmaForm");
  const miFirmaAviso = document.getElementById("espacioMiFirmaAviso");
  const miFirmaCanvas = document.getElementById("miFirmaCanvas");

  document.querySelectorAll("[data-mi-firma]").forEach((boton) => {
    boton.addEventListener("click", () => {
      mostrarAviso(miFirmaAviso, "", false);
      abrirModal(miFirmaModal);
      // El canvas debe medirse cuando el modal ya es visible.
      window.requestAnimationFrame(() => {
        const pad = pads.get("miFirmaCanvas");
        const inicial = miFirmaCanvas?.dataset.firmaInicial;
        if (inicial) {
          pad?.cargar(inicial);
        } else {
          pad?.ajustar();
        }
      });
    });
  });

  miFirmaForm?.addEventListener("submit", async (evento) => {
    evento.preventDefault();
    const pad = pads.get("miFirmaCanvas");

    if (!pad?.tieneTrazo()) {
      mostrarAviso(miFirmaAviso, "Traza tu firma antes de guardarla.", true);
      return;
    }

    miFirmaForm.querySelector('[name="FirmaDataUrl"]').value = pad.obtener();

    try {
      const respuesta = await fetch("/EspacioCorporativo/GuardarMiFirma", {
        method: "POST",
        headers: { RequestVerificationToken: obtenerToken() },
        body: new FormData(miFirmaForm)
      });
      const datos = await respuesta.json();

      if (!respuesta.ok) {
        mostrarAviso(miFirmaAviso, datos.message || "No se pudo guardar la firma.", true);
        return;
      }

      mostrarAviso(miFirmaAviso, datos.mensaje, false);
      window.setTimeout(() => window.location.reload(), 900);
    } catch (error) {
      console.error(error);
      mostrarAviso(miFirmaAviso, "No fue posible guardar la firma.", true);
    }
  });

  document.getElementById("espacioMiFirmaEliminar")?.addEventListener("click", async () => {
    if (!window.confirm("¿Eliminar tu firma guardada? Deberás trazarla de nuevo en la próxima acta.")) {
      return;
    }

    try {
      const respuesta = await fetch("/EspacioCorporativo/EliminarMiFirma", {
        method: "POST",
        headers: { RequestVerificationToken: obtenerToken() }
      });
      if (!respuesta.ok) throw new Error("fallo");
      window.location.reload();
    } catch (error) {
      console.error(error);
      mostrarAviso(miFirmaAviso, "No fue posible eliminar la firma.", true);
    }
  });

  // ── Acta de entrega / devolución ──────────────────────────────────────────

  const actaModal = document.getElementById("espacioActaModal");
  const actaForm = document.getElementById("espacioActaForm");
  const actaAviso = document.getElementById("espacioActaAviso");
  const actaTitulo = document.getElementById("espacioActaTitulo");
  const actaSubtitulo = document.getElementById("espacioActaSubtitulo");
  const actaEquipo = document.getElementById("espacioActaEquipo");
  const actaFirmaGuardada = document.getElementById("espacioActaFirmaGuardada");
  const actaFirmaPad = document.getElementById("espacioActaFirmaPad");
  const actaHistorialWrap = document.getElementById("espacioActaHistorialWrap");
  const actaHistorial = document.getElementById("espacioActaHistorial");

  const campoActa = (nombre) => actaForm?.querySelector(`[name="${nombre}"]`);

  function pintarDatoEquipo(etiqueta, valor) {
    if (!valor) return "";
    const bloque = document.createElement("div");
    bloque.className = "espacio-field";
    const titulo = document.createElement("span");
    titulo.textContent = etiqueta;
    const dato = document.createElement("strong");
    dato.textContent = valor;
    bloque.appendChild(titulo);
    bloque.appendChild(dato);
    return bloque.outerHTML;
  }

  document.querySelectorAll("[data-acta-abrir]").forEach((boton) => {
    boton.addEventListener("click", async () => {
      const activoId = boton.getAttribute("data-acta-abrir");
      mostrarAviso(actaAviso, "", false);

      try {
        const respuesta = await fetch(`/EspacioCorporativo/Acta?id=${encodeURIComponent(activoId)}`, {
          headers: { Accept: "application/json" }
        });
        if (!respuesta.ok) throw new Error("fallo");
        const datos = await respuesta.json();

        const esDevolucion = datos.siguienteTipo === "Devolución";
        campoActa("ActivoId").value = datos.activo.id;
        campoActa("Tipo").value = datos.siguienteTipo;
        campoActa("RecibePorNombre").value = esDevolucion ? "" : datos.activo.responsable || "";
        campoActa("RecibePorDocumento").value = "";
        campoActa("Observaciones").value = "";

        actaTitulo.textContent = esDevolucion ? "Acta de devolución" : "Acta de entrega";
        actaSubtitulo.textContent = esDevolucion
          ? "Registra la devolución del equipo con la firma de quien lo entrega"
          : "Deja constancia firmada de la entrega del equipo";

        actaEquipo.innerHTML = [
          pintarDatoEquipo("Equipo", datos.activo.descripcion),
          pintarDatoEquipo("Serial", datos.activo.serial),
          pintarDatoEquipo("Codigo", datos.activo.codigo),
          pintarDatoEquipo("Responsable", datos.activo.responsable)
        ].join("");

        // La firma de TI solo se pide cuando aún no hay una guardada.
        const tieneFirma = Boolean(datos.firmaGuardada);
        actaFirmaGuardada.hidden = !tieneFirma;
        actaFirmaPad.hidden = tieneFirma;
        document.querySelector('[data-firma-limpiar="actaEntregaCanvas"]').hidden = tieneFirma;

        if (tieneFirma) {
          actaFirmaGuardada.querySelector("img").src = datos.firmaGuardada.dataUrl;
          document.getElementById("espacioActaFirmaNombre").textContent =
            [datos.firmaGuardada.nombre, datos.firmaGuardada.cargo].filter(Boolean).join(" · ");
        }

        if (datos.actas.length > 0) {
          actaHistorialWrap.hidden = false;
          actaHistorial.innerHTML = datos.actas
            .map((acta) => {
              const item = document.createElement("div");
              item.className = "espacio-timeline__item";
              const titulo = document.createElement("strong");
              titulo.textContent = `${acta.tipo} — ${acta.recibePor}`;
              const meta = document.createElement("span");
              meta.textContent = `${acta.fecha} · entrega ${acta.entregaPor}`;
              const enlace = document.createElement("a");
              enlace.href = `/EspacioCorporativo/ActaDocumento?id=${acta.id}`;
              enlace.target = "_blank";
              enlace.rel = "noopener";
              enlace.className = "espacio-acta-enlace";
              enlace.textContent = "Ver acta";
              item.appendChild(titulo);
              item.appendChild(meta);
              item.appendChild(enlace);
              return item.outerHTML;
            })
            .join("");
        } else {
          actaHistorialWrap.hidden = true;
          actaHistorial.innerHTML = "";
        }

        abrirModal(actaModal);
        window.requestAnimationFrame(() => {
          pads.get("actaRecibeCanvas")?.limpiar();
          pads.get("actaRecibeCanvas")?.ajustar();
          if (!tieneFirma) {
            pads.get("actaEntregaCanvas")?.limpiar();
            pads.get("actaEntregaCanvas")?.ajustar();
          }
        });
      } catch (error) {
        console.error(error);
        window.alert("No fue posible cargar la información del acta.");
      }
    });
  });

  // ── Módulo de Actas: mostrar credenciales y firmar el documento ───────────

  document.querySelectorAll("[data-ver-credencial]").forEach((boton) => {
    boton.addEventListener("click", () => {
      const campo = document.getElementById(boton.dataset.verCredencial);
      if (!campo) return;
      const oculto = campo.type === "password";
      campo.type = oculto ? "text" : "password";
      boton.querySelector("i")?.classList.toggle("bi-eye-fill", !oculto);
      boton.querySelector("i")?.classList.toggle("bi-eye-slash-fill", oculto);
    });
  });

  const emitirForm = document.getElementById("espacioActaEmitirForm");
  const emitirAviso = document.getElementById("espacioEmitirAviso");

  if (emitirForm) {
    // Un acta puede llevar más de dos firmas: cada canvas declara a qué firma
    // pertenece y dónde se refleja dentro de la previsualización del documento.
    const canvasFirmas = Array.from(emitirForm.querySelectorAll("canvas[data-firma-clave]"));

    // Los pads de esta pantalla ya son visibles al cargar: se miden de una vez.
    window.requestAnimationFrame(() => {
      canvasFirmas.forEach((canvas) => pads.get(canvas.id)?.ajustar());
    });

    canvasFirmas.forEach((canvas) => {
      const contenedor = emitirForm.querySelector(
        '[data-firma-vista="' + canvas.dataset.firmaClave + '"]'
      );
      if (!contenedor) return;

      canvas.addEventListener("pointerup", () => {
        const dataUrl = pads.get(canvas.id)?.obtener();
        if (!dataUrl) return;

        let imagen = contenedor.querySelector("img");
        if (!imagen) {
          imagen = document.createElement("img");
          imagen.alt = "Firma";
          contenedor.appendChild(imagen);
        }
        imagen.src = dataUrl;
      });
    });

    emitirForm.addEventListener("submit", (evento) => {
      for (const canvas of canvasFirmas) {
        const pad = pads.get(canvas.id);
        const requerida = canvas.dataset.firmaRequerida === "true";

        if (requerida && !pad?.tieneTrazo()) {
          evento.preventDefault();
          mostrarAviso(emitirAviso, "Falta la firma de " + canvas.dataset.firmaRotulo + ".", true);
          canvas.scrollIntoView({ behavior: "smooth", block: "center" });
          return;
        }

        const destino = emitirForm.querySelector(
          '[data-firma-destino="' + canvas.dataset.firmaClave + '"]'
        );
        if (destino) destino.value = pad?.tieneTrazo() ? pad.obtener() : "";
      }

      const boton = document.getElementById("espacioEmitirBoton");
      if (boton) {
        boton.disabled = true;
        boton.textContent = "Firmando...";
      }
    });
  }

  actaForm?.addEventListener("submit", async (evento) => {
    evento.preventDefault();
    mostrarAviso(actaAviso, "", false);

    const padRecibe = pads.get("actaRecibeCanvas");
    if (!padRecibe?.tieneTrazo()) {
      mostrarAviso(actaAviso, "Falta la firma de quien recibe.", true);
      return;
    }

    if (!campoActa("RecibePorNombre").value.trim()) {
      mostrarAviso(actaAviso, "Indica el nombre de quien recibe.", true);
      return;
    }

    campoActa("FirmaRecibeDataUrl").value = padRecibe.obtener();

    if (!actaFirmaPad.hidden) {
      const padEntrega = pads.get("actaEntregaCanvas");
      if (!padEntrega?.tieneTrazo()) {
        mostrarAviso(actaAviso, "Traza tu firma de entrega para continuar.", true);
        return;
      }
      campoActa("FirmaEntregaDataUrl").value = padEntrega.obtener();
    }

    const boton = document.getElementById("espacioActaGuardar");
    boton.disabled = true;

    try {
      const respuesta = await fetch("/EspacioCorporativo/FirmarActa", {
        method: "POST",
        headers: { RequestVerificationToken: obtenerToken() },
        body: new FormData(actaForm)
      });
      const datos = await respuesta.json();

      if (!respuesta.ok) {
        mostrarAviso(actaAviso, datos.message || "No se pudo firmar el acta.", true);
        return;
      }

      mostrarAviso(actaAviso, `${datos.mensaje} Abriendo el acta...`, false);
      window.open(datos.urlActa, "_blank", "noopener");
      window.setTimeout(() => window.location.reload(), 1200);
    } catch (error) {
      console.error(error);
      mostrarAviso(actaAviso, "No fue posible firmar el acta.", true);
    } finally {
      boton.disabled = false;
    }
  });
})();
