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
          historialContenido.innerHTML = "<p>Este activo aun no registra movimientos.</p>";
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
})();
