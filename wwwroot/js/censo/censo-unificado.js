// Script de la pantalla única (navegación, confirmaciones, datos básicos) en la pantalla única del censo.
//
// Vivía en línea en Views/Censo/Index.cshtml y se sacó a este archivo (2026-09-24) para que el navegador lo
// guarde en caché: en línea se descargaba y se volvía a analizar en cada consulta de un paciente.
// Los valores que dependen del servidor (URLs, permisos, datos del registro) los deja esa vista en
// window.NexaCenso.unificado justo antes de cargar este archivo.
(() => {
    const programaPorDefecto = NexaCenso.unificado.programaPorDefecto;
    const validarDireccionUrl = NexaCenso.unificado.validarDireccionUrl;
    const buscarBarriosUrl = NexaCenso.unificado.buscarBarriosUrl;
    const defaultsMunicipioUrl = NexaCenso.unificado.defaultsMunicipioUrl;
    const buscarDiagnosticoCie10Url = NexaCenso.unificado.buscarDiagnosticoCie10Url;
    const verificarPacienteMaestroUrl = NexaCenso.unificado.verificarPacienteMaestroUrl;

    // ----- Mayúsculas en todo lo que se escribe -----
    // Los cinco censos ya lo hacían dentro de su propio formulario; al subir la recepción y
    // los datos básicos al maestro, ese formulario se quedó sin la regla. Misma mecánica:
    // solo campos de texto, y se conserva la posición del cursor para no estorbar al escribir.
    const esCampoDeTexto = (control) => {
        if (!(control instanceof HTMLInputElement) && !(control instanceof HTMLTextAreaElement)) {
            return false;
        }
        if (control instanceof HTMLTextAreaElement) { return true; }
        const tipo = String(control.getAttribute("type") || "text").toLowerCase();
        return ["text", "search", "email", "tel", "url"].includes(tipo);
    };

    const aMayusculas = (control) => {
        if (!esCampoDeTexto(control)) { return; }
        const actual = control.value || "";
        const mayuscula = actual.toLocaleUpperCase("es-CO");
        if (actual === mayuscula) { return; }

        const ini = control.selectionStart;
        const fin = control.selectionEnd;
        control.value = mayuscula;
        if (ini !== null && fin !== null) {
            try { control.setSelectionRange(ini, fin); } catch { }
        }
    };

    const ambitoMayusculas = "#censoPacienteForm, .censo-identidad__buscador";

    document.addEventListener("input", (evento) => {
        const control = evento.target;
        if (!(control instanceof HTMLElement) || !control.closest(ambitoMayusculas)) { return; }
        aMayusculas(control);
    });

    document.querySelectorAll(`${ambitoMayusculas} input, ${ambitoMayusculas} textarea`)
        .forEach(aMayusculas);

    // ----- Número de identificación según su tipo -----
    // Solo dígitos, salvo pasaporte y cédula de extranjería, que sí llevan letras. Es la
    // misma lista que usan los cinco programas.
    const tiposConLetras = new Set(["PA", "CE"]);
    const tipoIdentificacionMaestro = document.getElementById("tipoIdentificacionInput");
    const numeroIdentificacionMaestro = document.getElementById("numeroIdentificacionMaestroInput");

    const sincronizarNumeroIdentificacion = () => {
        if (!numeroIdentificacionMaestro) { return; }

        const admiteLetras = tiposConLetras.has(
            String(tipoIdentificacionMaestro?.value || "").trim().toUpperCase());

        numeroIdentificacionMaestro.setAttribute("inputmode", admiteLetras ? "text" : "numeric");
        numeroIdentificacionMaestro.title = admiteLetras
            ? "Permite letras y números para PA o CE."
            : "Solo permite dígitos para este tipo de identificación.";

        const actual = String(numeroIdentificacionMaestro.value || "").toUpperCase();
        numeroIdentificacionMaestro.value = admiteLetras
            ? actual.replace(/[^A-Z0-9]/g, "").slice(0, 20)
            : actual.replace(/\D/g, "").slice(0, 20);
    };

    tipoIdentificacionMaestro?.addEventListener("change", sincronizarNumeroIdentificacion);
    numeroIdentificacionMaestro?.addEventListener("input", sincronizarNumeroIdentificacion);
    sincronizarNumeroIdentificacion();

    // ----- Avisar si el documento ya tiene paciente maestro -----
    // GuardarAsync busca siempre por documento y actualiza el maestro que encuentre, así
    // que si aquí se escribe por error el número de otro paciente real, hoy se le pisarían
    // sus datos sin ningún aviso. Se consulta al escribir (con espera) y al salir del
    // campo, para que también dispare cuando el extractor de remisión llena el documento
    // (dispara "change" igual que si lo escribiera una persona). No bloquea el guardado:
    // puede ser el mismo paciente que ya se está editando, o un dato real que se corrige.
    const pacienteIdMaestroInput = document.getElementById("Paciente_PacienteId");
    const mensajeDocumentoMaestro = document.getElementById("documentoMaestroMensaje");

    const mostrarMensajeDocumento = (texto, tono) => {
        if (!mensajeDocumentoMaestro) return;
        if (!texto) {
            mensajeDocumentoMaestro.className = "alert d-none mt-2 py-2 px-3";
            mensajeDocumentoMaestro.textContent = "";
            return;
        }
        mensajeDocumentoMaestro.textContent = texto;
        mensajeDocumentoMaestro.className = `alert mt-2 py-2 px-3 alert-${tono}`;
    };

    const verificarDocumentoMaestro = async () => {
        const documento = String(numeroIdentificacionMaestro?.value || "").trim();
        if (documento.length < 5) { mostrarMensajeDocumento("", null); return; }

        let datos = null;
        try {
            const respuesta = await fetch(
                `${verificarPacienteMaestroUrl}?documento=${encodeURIComponent(documento)}`);
            if (respuesta.ok) { datos = await respuesta.json(); }
        } catch { /* sin conexion no se estorba la escritura */ }

        if (!datos?.existe) { mostrarMensajeDocumento("", null); return; }

        const idActual = String(pacienteIdMaestroInput?.value || "").trim();
        if (idActual && String(datos.pacienteId) === idActual) {
            // Es el mismo paciente que ya se está editando: nada que avisar.
            mostrarMensajeDocumento("", null);
            return;
        }

        mostrarMensajeDocumento(
            `Este documento ya pertenece a un paciente registrado${datos.nombre ? `: ${datos.nombre}` : ""}.`
            + " Si guardas, se actualizarán sus datos.",
            "warning");
    };

    let temporizadorDocumentoMaestro = null;
    numeroIdentificacionMaestro?.addEventListener("input", () => {
        clearTimeout(temporizadorDocumentoMaestro);
        temporizadorDocumentoMaestro = setTimeout(verificarDocumentoMaestro, 500);
    });
    numeroIdentificacionMaestro?.addEventListener("change", verificarDocumentoMaestro);
    if (numeroIdentificacionMaestro?.value) { verificarDocumentoMaestro(); }

    // El buscador recibe un documento: mismas reglas de caracteres.
    const buscarDocumento = document.getElementById("buscarDocumento");
    buscarDocumento?.addEventListener("input", () => {
        buscarDocumento.value = String(buscarDocumento.value || "")
            .toUpperCase().replace(/[^A-Z0-9]/g, "");
    });

    // ----- Confirmación compartida -----
    // Devuelve una promesa con la decisión, para usarla donde antes iría un confirm(). La
    // promesa se resuelve cuando el modal termina de ocultarse y no al pulsar el botón: si
    // se resolviera antes, un diálogo encadenado se abriría a mitad de la animación de
    // cierre y Bootstrap lo descartaría. La exponen en window porque también la usan los
    // parciales de programa, que se renderizan antes que este bloque.
    // ----- Aviso de éxito como toast -----
    // Reemplaza el banner verde grande de antes: mismo mensaje, pero se auto-oculta en
    // vez de quedarse fijo hasta la siguiente navegación.
    const toastExitoEl = document.getElementById("censoToastExito");
    if (toastExitoEl && toastExitoEl.querySelector(".toast-body")?.textContent.trim()) {
        new bootstrap.Toast(toastExitoEl, { delay: 5000 }).show();
    }

    const confirmarEl = document.getElementById("censoConfirmarDialogo");
    const confirmarModal = confirmarEl ? new bootstrap.Modal(confirmarEl) : null;

    // Para resaltar en negrilla el nombre del paciente o el programa dentro del mensaje:
    // el mensaje ahora se pinta con innerHTML, así que cada dato dinámico que lo compone
    // se escapa primero con este helper y solo las etiquetas <strong> que agrega el propio
    // código quedan como HTML real.
    window.censoEscapeHtml = (valor) => {
        const contenedor = document.createElement("div");
        contenedor.textContent = String(valor ?? "");
        return contenedor.innerHTML;
    };

    window.censoConfirmar = ({ titulo, mensaje, textoConfirmar = "Confirmar", tono = "aviso" }) =>
        new Promise((resolver) => {
            if (!confirmarModal) {
                resolver(window.confirm(mensaje.replace(/<\/?strong>/g, "")));
                return;
            }

            const aceptar = document.getElementById("censoConfirmarAceptar");
            document.getElementById("censoConfirmarTitulo").textContent = titulo;
            document.getElementById("censoConfirmarMensaje").innerHTML = mensaje;
            confirmarEl.dataset.tono = tono;
            aceptar.textContent = textoConfirmar;
            aceptar.className = tono === "peligro"
                ? "btn btn-danger"
                : tono === "advertencia" ? "btn btn-warning" : "btn btn-primary";

            let confirmado = false;
            let resuelto = false;

            const alConfirmar = () => {
                confirmado = true;
                confirmarModal.hide();
            };

            const alOcultar = () => {
                if (resuelto) { return; }
                resuelto = true;
                aceptar.removeEventListener("click", alConfirmar);
                confirmarEl.removeEventListener("hidden.bs.modal", alOcultar);
                resolver(confirmado);
            };

            aceptar.addEventListener("click", alConfirmar);
            confirmarEl.addEventListener("hidden.bs.modal", alOcultar);
            confirmarModal.show();
        });

    // ----- Atención cerrada: secciones que ya no se editan -----
    // El panel de un programa cuya atención seleccionada está cerrada llega marcado con
    // data-solo-lectura. Aquí se apagan sus secciones, salvo las que por diseño se
    // diligencian después del alta.
    //
    // Los campos NO se deshabilitan: un campo deshabilitado no se envía, y las acciones de
    // sección reciben el modelo completo, así que apagarlos borraría del registro lo que
    // no se estaba tocando. Se dejan visibles y con su valor —una atención cerrada se abre
    // justamente para leerla— pero fuera del alcance del ratón y del tabulador. El candado
    // real es del servidor: CensoAtencionCerradaFilter y RevertirCamposBloqueadosDeAgudos.
    (() => {
        const editablesTrasAlta = new Set(NexaCenso.unificado.editablesTrasAlta);

        document.querySelectorAll('.censo-programa-panes [data-solo-lectura="true"]')
            .forEach((pane) => {
                pane.querySelectorAll(".tab-pane[id^='tab-']").forEach((seccion) => {
                    if (editablesTrasAlta.has(seccion.id)) { return; }

                    seccion.classList.add("censo-seccion--cerrada");
                    seccion.querySelectorAll("input, select, textarea").forEach((campo) => {
                        if (campo.type === "hidden") { return; }
                        campo.setAttribute("tabindex", "-1");
                        campo.setAttribute("aria-readonly", "true");
                    });
                    // Guardar esta sección no haría nada: el servidor la rechaza. Se quita
                    // el botón en vez de dejar que alguien lo pulse y reciba un error.
                    seccion.querySelectorAll(".censo-save-button").forEach((boton) => {
                        boton.remove();
                    });
                });
            });
    })();

    // El nombre del paciente para los mensajes de confirmación. Vive en el maestro, así que
    // sirve para cualquier programa.
    window.censoNombrePaciente = () =>
        document.getElementById("Paciente_NombrePaciente")?.value?.trim() || "este paciente";

    // ----- Confirmar antes de reabrir una atencion -----
    // Reabrir devuelve la atencion a activa y borra su alta o su egreso. Se pregunta
    // porque es un cambio de estado del paciente y no hay como deshacerlo con un clic.
    document.querySelectorAll("[data-censo-reabrir]").forEach((formulario) => {
        formulario.addEventListener("submit", async (evento) => {
            evento.preventDefault();
            const programa = formulario.dataset.censoReabrir;
            const numero = formulario.dataset.censoReabrirNumero;
            const confirmado = await window.censoConfirmar({
                titulo: "Reabrir atencion",
                mensaje: `Vas a reabrir la atencion <strong>${window.censoEscapeHtml(numero)}</strong> de `
                    + `<strong>${window.censoEscapeHtml(programa)}</strong> de `
                    + `<strong>${window.censoEscapeHtml(window.censoNombrePaciente())}</strong>. Volvera a `
                    + `estar activa y se retirara su alta, de modo que podra editarse otra vez.`
            });
            if (confirmado) { formulario.submit(); }
        });
    });

    // ----- Confirmar antes de agregar un programa -----
    // El carril agrega el programa con un envío directo; se intercepta para preguntar
    // primero. form.submit() no vuelve a disparar este manejador, así que no hay recursión.
    document.querySelectorAll("[data-censo-agregar-programa]").forEach((formulario) => {
        formulario.addEventListener("submit", async (evento) => {
            evento.preventDefault();
            const programa = formulario.dataset.censoAgregarPrograma;
            const aviso = formulario.dataset.censoAgregarAviso;
            const confirmado = await window.censoConfirmar({
                titulo: "Agregar programa",
                mensaje: `¿Estás seguro de agregarle al paciente <strong>${window.censoEscapeHtml(window.censoNombrePaciente())}</strong> `
                    + `el siguiente programa: <strong>${window.censoEscapeHtml(programa)}</strong>?`
                    + (aviso ? ` ${window.censoEscapeHtml(aviso)}` : "")
            });
            if (!confirmado) { return; }
            // El servidor exige esta marca: un envío que no pasó por aquí no agrega nada.
            const marca = formulario.querySelector("[name='confirmado']");
            if (marca) { marca.value = "true"; }
            formulario.submit();
        });
    });

    // ----- Avisar si el paciente ya existe en ese programa -----
    // Al crear el registro de un programa se consulta si ese documento ya tiene uno. No se
    // bloquea —un paciente puede reingresar y tener varias atenciones— pero sí se avisa,
    // porque un duplicado por descuido obliga después a borrar datos de producción.
    const verificarPacienteUrl = NexaCenso.unificado.verificarPacienteUrl;

    // La recepción queda fuera: guarda contra un ingreso que ya existe, así que no puede
    // crear un duplicado. Hoy además se saldría sola del guard —no lleva el documento en
    // el formulario—, pero eso es una casualidad y no una regla.
    document
        .querySelectorAll(".censo-programa-panes [data-programa] form.censo-form-shell:not(.censo-recepcion-form)")
        .forEach((formulario) => {
            const programa = formulario.closest("[data-programa]")?.dataset.programa;

            formulario.addEventListener("submit", async (evento) => {
                // Solo al crear: si ya se está editando un registro no hay duplicado posible.
                const editando = formulario.querySelector("[name='EditingRecordId']")?.value;
                if (editando || formulario.dataset.duplicadoConfirmado === "si") { return; }

                const documento = formulario.querySelector("[name='NumeroIdentificacion']")?.value?.trim();
                if (!documento || !programa) { return; }

                evento.preventDefault();

                let datos = null;
                try {
                    const respuesta = await fetch(
                        `${verificarPacienteUrl}?documento=${encodeURIComponent(documento)}`
                        + `&programa=${encodeURIComponent(programa)}`);
                    if (respuesta.ok) { datos = await respuesta.json(); }
                } catch { /* sin conexión no se estorba el guardado */ }

                if (datos?.existe) {
                    const cuantos = datos.total === 1
                        ? "1 registro"
                        : `${datos.total} registros`;
                    const abiertos = datos.abiertos > 0
                        ? ` ${datos.abiertos === 1 ? "Uno sigue activo" : `${datos.abiertos} siguen activos`}.`
                        : "";
                    const confirmado = await window.censoConfirmar({
                        titulo: "El paciente ya existe",
                        mensaje: `El documento <strong>${window.censoEscapeHtml(documento)}</strong> ya tiene ${cuantos} `
                            + `en <strong>${window.censoEscapeHtml(datos.programa)}</strong>`
                            + `${datos.nombre ? ` (<strong>${window.censoEscapeHtml(datos.nombre)}</strong>)` : ""}.${abiertos}`
                            + " ¿Confirmas que quieres crear otro registro?",
                        textoConfirmar: "Crear de todos modos",
                        tono: "advertencia"
                    });
                    if (!confirmado) { return; }
                }

                formulario.dataset.duplicadoConfirmado = "si";
                formulario.requestSubmit();
            });
        });

    // ----- Grupos plegables del navegador -----
    // Solo se despliega el grupo de la sección en la que estás. Con los cinco programas
    // abiertos el navegador mostraba quince secciones de una vez y costaba encontrar nada.
    const plegarGrupo = (grupo, abrir) => {
        grupo.dataset.abierto = abrir ? "true" : "false";
        grupo.querySelector("[data-censo-grupo-toggle]")
            ?.setAttribute("aria-expanded", abrir ? "true" : "false");
    };

    document.querySelectorAll("[data-censo-grupo-toggle]").forEach((cabecera) => {
        cabecera.addEventListener("click", () => {
            const grupo = cabecera.closest("[data-censo-grupo]");
            if (grupo) { plegarGrupo(grupo, grupo.dataset.abierto !== "true"); }
        });
    });

    // Al saltar a una sección desde otro sitio (una validación que manda a "Plan de manejo",
    // por ejemplo) su grupo tiene que abrirse solo; si no, el clic no llevaría a ninguna parte.
    const abrirGrupoDeSeccion = (id) => {
        const grupo = document.getElementById(id)?.closest("[data-programa]");
        const nombrePrograma = grupo?.dataset.programa;
        const destino = nombrePrograma
            ? document.querySelector(`.censo-nav-grupo[data-programa="${nombrePrograma}"]`)
            : document.querySelector(`[data-censo-tab="${id}"]`)?.closest("[data-censo-grupo]");
        if (!destino || destino.dataset.abierto === "true") { return; }
        plegarGrupo(destino, true);
    };

    // ----- Navegación por secciones -----
    // Los paneles viven en dos contenedores distintos (el formulario del paciente y el bloque
    // de programas), así que el cambio de pestaña se hace por id en lugar de delegarlo al
    // plugin de Bootstrap, que solo alterna entre hermanos.
    const nav = document.getElementById("censoUnificadoNav");
    const activarSeccion = (id) => {
        if (!id) return;
        abrirGrupoDeSeccion(id);

        document.querySelectorAll("[data-censo-tab]").forEach((boton) => {
            const activo = boton.dataset.censoTab === id;
            boton.classList.toggle("active", activo);
            boton.setAttribute("aria-selected", activo ? "true" : "false");
        });
        document.querySelectorAll(".tab-pane[id^='tab-']").forEach((panel) => {
            const activo = panel.id === id;
            panel.classList.toggle("show", activo);
            panel.classList.toggle("active", activo);
        });

        // La barra de guardado de cada programa vive fuera de sus secciones, así que se
        // quedaba a la vista aunque estuvieras en una sección de otro programa: en clínica
        // de heridas se veía "Gestionar kardex y requisición", que es de agudos. Solo se
        // muestra la del programa dueño de la sección activa. Las barras de sección
        // (--inline) no entran: esas ya se ocultan con su propia sección.
        //
        // La recepción es la excepción: está dentro del panel del programa pero es otro
        // formulario, con su propio botón. Dejar la barra del programa a la vista ofrecía
        // ahí "Gestionar kardex y requisición" y "Actualizar registro", que no son de esta
        // sección y actuaban sobre el registro del censo, no sobre la recepción.
        const seccionActivaEl = document.getElementById(id);
        const paneActivo = seccionActivaEl?.closest("[data-programa]");
        const enRecepcion = seccionActivaEl?.dataset.censoRecepcion === "true";
        document
            .querySelectorAll(".censo-programa-panes [data-programa] .censo-form-actions:not(.censo-form-actions--inline)")
            .forEach((barra) => {
                barra.classList.toggle(
                    "d-none", enRecepcion || barra.closest("[data-programa]") !== paneActivo);
            });

        // La banda de atencion vive en la ficha del paciente, arriba, pero habla del
        // programa que se este editando: hay una por programa y se muestra la del activo.
        // En una seccion del paciente no hay programa activo; se muestra la del
        // programa por defecto para no dejar al paciente sin su estado a la vista.
        const programaActivo = paneActivo?.dataset.programa ?? programaPorDefecto;
        document.querySelectorAll("[data-censo-banda-atencion]").forEach((banda) => {
            banda.classList.toggle("d-none", banda.dataset.programa !== programaActivo);
        });
        const url = new URL(window.location.href);
        url.searchParams.set("seccion", id);
        window.history.replaceState({}, "", url);
    };

    nav?.addEventListener("click", (evento) => {
        const boton = evento.target.closest("[data-censo-tab]");
        if (!boton) return;
        evento.preventDefault();
        // Una seccion puede quedar bloqueada por su programa (gestion de alta solo cuando el
        // paciente esta en alta). Antes lo impedia Bootstrap al quitarle el data-bs-toggle.
        if (boton.getAttribute("aria-disabled") === "true") return;
        activarSeccion(boton.dataset.censoTab);
    });

    // ----- Salvaguarda de los modales -----
    // .censo-page declara isolation: isolate, así que crea su propio contexto de apilamiento.
    // Un modal declarado dentro queda atrapado ahí mientras Bootstrap cuelga el fondo oscuro
    // del body: el fondo termina encima y la pantalla se ve negra, sin forma de cerrar. Los
    // modales se declaran fuera de esa capa; esto solo rescata alguno que se cuele después.
    const modalesExtraviados = document.querySelectorAll(".censo-page .modal");
    modalesExtraviados.forEach((modal) => document.body.appendChild(modal));
    if (modalesExtraviados.length) {
        console.warn(
            `Censo: ${modalesExtraviados.length} modal(es) estaban dentro de .censo-page y se movieron al body.`);
    }

    // ----- Hora en formato 24h -----
    // Mismo comportamiento que tenia el censo de agudos: solo digitos, dos puntos
    // automaticos, hora tope 23 y aviso al salir si no quedo en HH:mm. Vive aqui y no en el
    // parcial de un programa porque la recepcion la tienen los cinco: se aplica por clase
    // sobre todo el documento, asi que alcanza tambien a la de cada ingreso.
    const aplicarMascaraHora = (campo) => {
        if (!campo || campo.dataset.horaLista === "1") return;
        campo.dataset.horaLista = "1";

        // ASP.NET escribe los TimeSpan como HH:mm:ss; en pantalla solo interesan hora y minuto.
        if (campo.value && campo.value.length > 5) campo.value = campo.value.substring(0, 5);

        campo.addEventListener("keydown", (evento) => {
            const navegacion = ["Backspace", "Delete", "Tab", "Escape", "Enter",
                "ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Home", "End"];
            if (navegacion.includes(evento.key)) return;
            if ((evento.ctrlKey || evento.metaKey) && ["a", "c", "v", "x"].includes(evento.key.toLowerCase())) return;
            if (evento.key === ":") return;
            if (!/^[0-9]$/.test(evento.key)) evento.preventDefault();
        });

        campo.addEventListener("input", () => {
            let digitos = campo.value.replace(/[^0-9]/g, "");
            if (digitos.length > 4) digitos = digitos.substring(0, 4);
            if (digitos.length >= 2 && parseInt(digitos.substring(0, 2), 10) > 23) {
                digitos = "23" + digitos.substring(2);
            }
            if (digitos.length === 4 && parseInt(digitos.substring(2), 10) > 59) {
                digitos = digitos.substring(0, 2) + "59";
            }
            campo.value = digitos.length > 2
                ? digitos.substring(0, 2) + ":" + digitos.substring(2)
                : digitos;
        });

        campo.addEventListener("blur", () => {
            const valido = !campo.value || /^([01][0-9]|2[0-3]):[0-5][0-9]$/.test(campo.value);
            campo.setCustomValidity(valido ? "" : "Ingresa la hora en formato HH:mm, por ejemplo 14:30.");
            if (!valido) campo.reportValidity();
        });

        campo.addEventListener("focus", () => campo.setCustomValidity(""));
    };
    document.querySelectorAll(".hora-24h-input").forEach(aplicarMascaraHora);

    // ----- Edad -----
    const fechaNacimiento = document.getElementById("fechaNacimientoMaestroInput");
    const edad = document.getElementById("edadMaestroInput");
    const calcularEdad = () => {
        if (!fechaNacimiento?.value || !edad) return;
        const nacimiento = new Date(fechaNacimiento.value + "T00:00:00");
        if (Number.isNaN(nacimiento.getTime())) return;
        const hoy = new Date();
        let anios = hoy.getFullYear() - nacimiento.getFullYear();
        const mes = hoy.getMonth() - nacimiento.getMonth();
        if (mes < 0 || (mes === 0 && hoy.getDate() < nacimiento.getDate())) anios -= 1;
        edad.value = Math.max(0, anios);
    };
    fechaNacimiento?.addEventListener("change", calcularEdad);

    // ----- Indicador de tiempo de respuesta -----
    // Hay una recepción por programa abierto, así que esto se engancha por formulario y no
    // por id: buscar los campos con getElementById devolvería siempre los del primer
    // programa del DOM y el indicador de los demás nunca se movería.
    //
    // Es solo un adelanto de lo que se ve en pantalla. El valor que se guarda lo calcula
    // el servidor en GuardarRecepcion, que es quien decide.
    document.querySelectorAll(".censo-recepcion-form").forEach((formulario) => {
        const campo = (nombre) => formulario.querySelector(`[data-recepcion="${nombre}"]`);
        const campos = ["fechaIngreso", "horaIngreso", "fechaRespuesta", "horaRespuesta"].map(campo);
        const indicador = campo("indicador");
        if (!indicador || campos.some((x) => !x)) return;

        const cifra = indicador.querySelector(".censo-recepcion__cifra");
        const unidad = indicador.querySelector(".censo-recepcion__unidad");

        const calcular = () => {
            const [fechaIngreso, horaIngreso, fechaRespuesta, horaRespuesta] = campos;
            if (!fechaIngreso.value || !horaIngreso.value || !fechaRespuesta.value || !horaRespuesta.value) return;
            const ingreso = new Date(`${fechaIngreso.value}T${horaIngreso.value}`);
            const respuesta = new Date(`${fechaRespuesta.value}T${horaRespuesta.value}`);
            if (Number.isNaN(ingreso.getTime()) || Number.isNaN(respuesta.getTime())) return;
            const minutos = Math.max(0, Math.round((respuesta - ingreso) / 60000));
            if (cifra) {
                cifra.textContent = minutos;
                cifra.classList.remove("censo-recepcion__cifra--vacia");
            }
            if (unidad) unidad.hidden = false;
        };

        campos.forEach((x) => x.addEventListener("change", calcular));
    });

    // ----- CIE-10 -----
    // El endpoint responde { found, codigo, diagnostico }. El diagnóstico es de solo lectura:
    // lo dicta el catálogo, no el usuario.
    const cie10 = document.getElementById("codigoCie10MaestroInput");
    const diagnostico = document.getElementById("diagnosticoMaestroInput");
    cie10?.addEventListener("blur", async () => {
        const codigo = (cie10.value || "").trim().toUpperCase();
        cie10.value = codigo;
        if (!diagnostico) return;
        if (!/^[A-Z][0-9]{3}$/.test(codigo)) {
            diagnostico.value = "";
            return;
        }
        try {
            const respuesta = await fetch(`${buscarDiagnosticoCie10Url}?codigo=${encodeURIComponent(codigo)}`);
            if (!respuesta.ok) return;
            const datos = await respuesta.json();
            diagnostico.value = datos?.found ? datos.diagnostico : "";
            if (datos?.found) {
                cie10.setCustomValidity("");
            } else {
                cie10.setCustomValidity("Ese código CIE10 no está en el catálogo.");
                cie10.reportValidity();
            }
        } catch { /* sin conexión: el usuario puede continuar y reintentar */ }
    });
    cie10?.addEventListener("focus", () => cie10.setCustomValidity(""));

    // La aplicación valida el token antifalsificación en todo POST, así que las llamadas
    // por fetch deben enviarlo en la cabecera igual que lo hacen los formularios.
    const tokenAntifalsificacion = () =>
        document.querySelector("input[name='__RequestVerificationToken']")?.value;

    // ----- Dirección -----
    // Mismo comportamiento que tenía el censo de agudos: se valida contra el servicio de
    // direcciones y con el resultado se completan municipio, barrio, zona Sura y zona según
    // municipio. El endpoint espera { Direccion } y responde con el municipio canónico, el
    // barrio sugerido, las zonas inferidas y la lista de barrios de ese municipio.
    const direccion = document.getElementById("direccionMaestroInput");
    const direccionEsValida = document.getElementById("direccionEsValidaInput");
    const mensajeDireccion = document.getElementById("direccionMaestroMensaje");
    const municipio = document.getElementById("municipioMaestroSelect");
    const barrio = document.getElementById("barrioMaestroInput");
    const barrioOpciones = document.getElementById("barrioMaestroOptions");
    const barrioAyuda = document.getElementById("barrioMaestroAyuda");
    const zonaDireccion = document.getElementById("zonaDireccionMaestroSelect");
    const zonaSura = document.getElementById("clasificacionZonaSuraMaestroSelect");
    const asumirErrada = document.querySelector('[name="Paciente.AsumirDireccionErrada"]');

    const mostrarMensaje = (texto, tono) => {
        if (!mensajeDireccion) return;
        mensajeDireccion.innerHTML = "";
        if (!texto) {
            mensajeDireccion.className = "alert d-none mt-2";
            return;
        }
        mensajeDireccion.textContent = texto;
        mensajeDireccion.className = `alert mt-2 alert-${tono}`;
    };

    const elegirOpcion = (select, valor) => {
        if (!select || !valor) return false;
        const objetivo = String(valor).trim().toUpperCase();
        const opcion = [...select.options].find((o) => o.value.trim().toUpperCase() === objetivo);
        if (!opcion) return false;
        select.value = opcion.value;
        return true;
    };

    // El barrio es un campo de escritura con sugerencias: con 262 barrios en Medellín un
    // desplegable obliga a recorrer la lista entera. Las sugerencias van en mayúscula porque
    // el campo también se escribe en mayúscula, y si no coincidieran el filtro del navegador
    // no encontraría nada.
    const llenarBarrios = (opciones, seleccionado) => {
        if (!barrioOpciones || !Array.isArray(opciones)) return;
        barrioOpciones.innerHTML = "";
        opciones
            .map((nombre) => String(nombre || "").trim())
            .filter((nombre) => nombre && nombre.toUpperCase() !== "NO PARAMETRIZADO")
            .forEach((nombre) => {
                const opcion = document.createElement("option");
                opcion.value = nombre.toLocaleUpperCase("es-CO");
                barrioOpciones.appendChild(opcion);
            });
        if (barrioAyuda) {
            barrioAyuda.textContent = barrioOpciones.options.length
                ? `${barrioOpciones.options.length} barrios disponibles. Escribe para filtrar.`
                : "Elige el municipio para ver sus barrios.";
        }
        if (seleccionado && barrio && !barrio.value) {
            barrio.value = String(seleccionado).toLocaleUpperCase("es-CO");
        }
    };

    // Al entrar, el paciente ya puede traer los barrios de su municipio: la ayuda dice
    // cuantos hay en vez de pedir que se elija un municipio que ya esta elegido.
    if (barrioAyuda && barrioOpciones?.options.length) {
        barrioAyuda.textContent =
            `${barrioOpciones.options.length} barrios disponibles. Escribe para filtrar.`;
    }

    // La zona según municipio depende del barrio: con el municipio solo, Medellín no se
    // puede resolver y queda en "No parametrizado". Al elegir barrio se vuelve a preguntar.
    const resolverZonaPorBarrio = async () => {
        const municipioActual = municipio?.value;
        const barrioActual = String(barrio?.value || "").trim();
        if (!municipioActual || !barrioActual) return;
        try {
            const respuesta = await fetch(
                `${defaultsMunicipioUrl}?municipio=${encodeURIComponent(municipioActual)}`
                + `&barrio=${encodeURIComponent(barrioActual)}`);
            if (!respuesta.ok) return;
            const datos = await respuesta.json();
            elegirOpcion(zonaSura, datos?.clasificacionZonaSura);
            elegirOpcion(zonaDireccion, datos?.zonaDireccionSegunMunicipio);
        } catch { /* las zonas quedan como están */ }
    };

    barrio?.addEventListener("change", resolverZonaPorBarrio);
    barrio?.addEventListener("blur", resolverZonaPorBarrio);

    // Los municipios fuera del catálogo fijo se resuelven contra el servicio de direcciones,
    // así que al escribir se vuelve a preguntar por ese término.
    let temporizadorBarrio = null;
    barrio?.addEventListener("input", () => {
        const termino = String(barrio.value || "").trim();
        const municipioActual = municipio?.value;
        if (termino.length < 3 || !municipioActual) return;
        clearTimeout(temporizadorBarrio);
        temporizadorBarrio = setTimeout(async () => {
            try {
                const respuesta = await fetch(
                    `${buscarBarriosUrl}?municipio=${encodeURIComponent(municipioActual)}`
                    + `&term=${encodeURIComponent(termino)}`);
                if (!respuesta.ok) return;
                const datos = await respuesta.json();
                const nuevos = (datos?.neighborhoods ?? [])
                    .map((x) => String(x || "").trim().toLocaleUpperCase("es-CO"))
                    .filter((x) => x && x !== "NO PARAMETRIZADO");
                const yaEstan = new Set([...barrioOpciones.options].map((o) => o.value));
                nuevos.filter((x) => !yaEstan.has(x)).forEach((x) => {
                    const opcion = document.createElement("option");
                    opcion.value = x;
                    barrioOpciones.appendChild(opcion);
                });
            } catch { /* se conserva lo que ya estaba sugerido */ }
        }, 300);
    });

    // Limpia lo que se dedujo de la direccion: al cambiarla, lo anterior ya no aplica.
    const limpiarDerivados = () => {
        [municipio, zonaSura, zonaDireccion].forEach((select) => {
            if (select) { select.value = ""; }
        });
        if (barrio) { barrio.value = ""; }
        llenarBarrios([], null);
        if (asumirErrada) { asumirErrada.checked = false; }
        if (direccionEsValida) { direccionEsValida.value = "false"; }
    };

    const limpiarDireccionCompleta = () => {
        if (direccion) { direccion.value = ""; }
        limpiarDerivados();
        mostrarMensaje("", null);
    };

    // Con el municipio ya resuelto se carga SU catalogo completo de barrios, no solo los que
    // devolvio la validacion: si no, el listado se quedaba en dos o tres y no habia forma de
    // elegir otro barrio del mismo municipio.
    const aplicarUbicacion = async (municipioDetectado, barrioDetectado) => {
        if (!municipioDetectado) { return; }
        if (!elegirOpcion(municipio, municipioDetectado)) { return; }

        try {
            const defaults = await fetch(
                `${defaultsMunicipioUrl}?municipio=${encodeURIComponent(municipioDetectado)}`
                + `&barrio=${encodeURIComponent(barrioDetectado || "")}`);
            if (defaults.ok) {
                const datos = await defaults.json();
                elegirOpcion(zonaSura, datos?.clasificacionZonaSura);
                elegirOpcion(zonaDireccion, datos?.zonaDireccionSegunMunicipio);
            }
        } catch { /* las zonas quedan como estan */ }

        try {
            const barrios = await fetch(
                `${buscarBarriosUrl}?municipio=${encodeURIComponent(municipioDetectado)}&term=a`);
            if (barrios.ok) {
                const datos = await barrios.json();
                llenarBarrios(datos?.neighborhoods ?? [], null);
            }
        } catch { /* se conserva lo que hubiera */ }

        if (barrio && barrioDetectado) {
            barrio.value = String(barrioDetectado).toLocaleUpperCase("es-CO");
        }
    };

    // Cuando la direccion da varias coincidencias se eligen; no se adivina por el usuario.
    const mostrarCoincidencias = (candidatos, mensaje) => {
        if (!mensajeDireccion) { return; }
        mostrarMensaje(mensaje || "Elige la direccion correcta entre las coincidencias.", "warning");

        candidatos.forEach((candidato) => {
            const boton = document.createElement("button");
            boton.type = "button";
            boton.className = "btn btn-sm btn-outline-secondary d-block text-start w-100 mt-1";
            boton.textContent = candidato.municipality
                ? `${candidato.formattedAddress} (${candidato.municipality})`
                : candidato.formattedAddress;
            boton.addEventListener("click", async () => {
                if (direccion && candidato.formattedAddress) {
                    direccion.value = candidato.formattedAddress;
                }
                await aplicarUbicacion(
                    candidato.municipality,
                    (candidato.neighborhood || candidato.district || "").trim());
                if (direccionEsValida) { direccionEsValida.value = "true"; }
                mostrarMensaje("Direccion seleccionada correctamente.", "success");
            });
            mensajeDireccion.appendChild(boton);
        });
    };

    const botonValidar = document.getElementById("validarDireccionMaestroBtn");

    const validarDireccion = async () => {
        const valor = (direccion?.value || "").trim();
        if (!valor) {
            mostrarMensaje("Escribe una direccion antes de validarla.", "warning");
            return;
        }

        if (botonValidar) { botonValidar.disabled = true; }
        mostrarMensaje("Validando direccion...", "secondary");

        try {
            const respuesta = await fetch(validarDireccionUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    ...(tokenAntifalsificacion() ? { RequestVerificationToken: tokenAntifalsificacion() } : {})
                },
                body: JSON.stringify({ Direccion: valor })
            });
            if (!respuesta.ok) {
                mostrarMensaje("No se pudo validar la direccion en este momento.", "warning");
                return;
            }

            const datos = await respuesta.json();
            if (direccionEsValida) { direccionEsValida.value = datos.isValid ? "true" : "false"; }

            const barrioDetectado = (datos.neighborhood || datos.district || "").trim();
            const sinResolver = datos.outcome === "invalid" || datos.outcome === "unavailable";
            const hayCoincidencias = datos.requiresSelection === true
                && Array.isArray(datos.candidates) && datos.candidates.length > 0;

            if (sinResolver) {
                limpiarDerivados();
            }

            if (!sinResolver && hayCoincidencias) {
                limpiarDerivados();
                mostrarCoincidencias(datos.candidates, datos.message);
                return;
            }

            if (!sinResolver) {
                await aplicarUbicacion(datos.municipality, barrioDetectado);
            }

            if (datos.isValid) {
                if (datos.formattedAddress && !asumirErrada?.checked && direccion) {
                    direccion.value = datos.formattedAddress;
                }
                mostrarMensaje(datos.message || "Direccion validada correctamente.", "success");
                return;
            }

            mostrarMensaje(
                `${datos.message || "No se pudo confirmar la direccion."}`
                + ' Corrige la direccion o marca "Asumir direccion errada y continuar".',
                "warning");

            if (datos.suggestedAddress && direccion) {
                const usar = document.createElement("button");
                usar.type = "button";
                usar.className = "btn btn-sm btn-link p-0 ms-2";
                usar.textContent = `Usar sugerencia: ${datos.suggestedAddress}`;
                usar.addEventListener("click", () => {
                    direccion.value = datos.suggestedAddress;
                    validarDireccion();
                });
                mensajeDireccion?.appendChild(usar);
            }
        } catch {
            limpiarDerivados();
            mostrarMensaje(
                "No fue posible validar la direccion. Intenta de nuevo o marca "
                + '"Asumir direccion errada y continuar".', "danger");
        } finally {
            if (botonValidar) { botonValidar.disabled = false; }
        }
    };

    document.getElementById("limpiarDireccionMaestroBtn")
        ?.addEventListener("click", limpiarDireccionCompleta);

    document.getElementById("validarDireccionMaestroBtn")?.addEventListener("click", validarDireccion);
    direccion?.addEventListener("blur", () => {
        if (direccion.value.trim() && direccionEsValida?.value !== "true" && !asumirErrada?.checked) {
            validarDireccion();
        }
    });

    // Al cambiar el municipio a mano se recalculan sus zonas y su lista de barrios.
    municipio?.addEventListener("change", async () => {
        const valor = municipio.value;
        if (!valor) return;
        try {
            const defaults = await fetch(`${defaultsMunicipioUrl}?municipio=${encodeURIComponent(valor)}`);
            if (defaults.ok) {
                const datos = await defaults.json();
                elegirOpcion(zonaSura, datos?.clasificacionZonaSura);
                elegirOpcion(zonaDireccion, datos?.zonaDireccionSegunMunicipio);
            }
            // Un término de una sola letra devuelve el catálogo completo del municipio.
            const barrios = await fetch(`${buscarBarriosUrl}?municipio=${encodeURIComponent(valor)}&term=a`);
            if (barrios.ok) {
                const datos = await barrios.json();
                // El barrio anterior es de otro municipio: se descarta al cambiar.
                if (barrio) { barrio.value = ""; }
                llenarBarrios(datos?.neighborhoods ?? [], null);
            }
        } catch { /* los catálogos quedan como están */ }
    });

    calcularEdad();

    // Los formularios de programa llegan desde parciales, sin panel marcado como activo:
    // el que se abre lo decide esta pantalla, con la seccion pedida por URL o la primera
    // del programa activo.
    const seccionActivaInicial = NexaCenso.unificado.seccionActivaInicial;
    activarSeccion(seccionActivaInicial);
})();
