// Televisor: reloj, cinta, actualización cada minuto y aviso de voz de la entrega de turno.
//
// - La hora sale del servidor (con el desfase que se mide en cada actualización), no del reloj del
//   televisor: un televisor con la hora corrida avisaría a destiempo.
// - El tablero se pide cada minuto y se reemplaza sin recargar la página. Recargar haría perder el
//   permiso de sonido que el navegador da después del primer toque, y el aviso no sonaría.
// - El aviso suena una sola vez por día (se recuerda en este televisor) entre la 1:30 y la 1:35 p. m.
//   de un día hábil. Las voces dependen del navegador: Edge trae voces naturales de Colombia.
// - Las preferencias (voz, mensaje, margen) son de este televisor: se guardan en el navegador.
// - Rotación: en un televisor se muestra un área a la vez (admisiones, analistas, novedades) en
//   letra grande, porque las tres juntas no se leían de lejos. El área visible es un atributo del
//   <html> y no del tablero, que se reemplaza en cada actualización.
(() => {
    "use strict";

    const cfg = window.NexaTelevisor || {};
    const contenedor = document.getElementById("tv-tablero");
    if (!contenedor) { return; }

    const INTERVALO_MS = 60_000;
    const VENTANA_AVISO_MS = 5 * 60_000;
    const CLAVE_PREFS = "nexa.televisor.v1";
    const CLAVE_AVISO = "nexa.televisor.aviso.";

    const MENSAJES = [
        {
            titulo: "Directo",
            texto: "Atención, equipo. Son la una y treinta. En quince minutos es el corte de turno. Dejen todo registrado y listo para la entrega de turno."
        },
        {
            titulo: "Breve",
            texto: "Recordatorio: faltan quince minutos para el corte de turno. Por favor, dejen todo preparado para la entrega."
        },
        {
            titulo: "Cordial",
            texto: "Buenas tardes, equipo. Es la una y media: preparemos la entrega de turno. Registren los ingresos pendientes; el corte es a la una y cuarenta y cinco."
        }
    ];

    // ------------------------------------------------------------------ preferencias

    const ESCENAS = ["admisiones", "analistas", "novedades"];
    // Admisiones es el área principal: se queda más del doble que las otras.
    const SEGUNDOS = { admisiones: 45, analistas: 20, novedades: 20 };

    const porOmision = {
        voz: "", mensaje: 0, volumen: 1, velocidad: 0.95, campana: true, repetir: true,
        margen: "normal", guias: false, tema: "oscuro", rotar: true, segundos: { ...SEGUNDOS }
    };
    const prefs = (() => {
        try { return { ...porOmision, ...JSON.parse(localStorage.getItem(CLAVE_PREFS) || "{}") }; }
        catch { return { ...porOmision }; }
    })();
    const guardarPrefs = () => {
        try { localStorage.setItem(CLAVE_PREFS, JSON.stringify(prefs)); } catch { /* sin almacenamiento: vale para esta sesión */ }
    };

    // ?tema=claro|oscuro|auto y ?rotar=1|0 en la dirección quedan fijos en este televisor.
    const pedidos = new URLSearchParams(location.search);
    const temaPedido = pedidos.get("tema");
    if (["oscuro", "claro", "auto"].includes(temaPedido) && prefs.tema !== temaPedido) {
        prefs.tema = temaPedido;
        guardarPrefs();
    }
    if (["0", "1"].includes(pedidos.get("rotar")) && prefs.rotar !== (pedidos.get("rotar") === "1")) {
        prefs.rotar = pedidos.get("rotar") === "1";
        guardarPrefs();
    }

    // ------------------------------------------------------------------ hora del servidor

    let desfase = Number(cfg.servidorMs || Date.now()) - Date.now();
    const ahora = () => Date.now() + desfase;
    const fmtHora = new Intl.DateTimeFormat("es-CO", { timeZone: "America/Bogota", hour: "numeric", minute: "2-digit", hour12: true });
    const partesHora = (ms) => {
        const partes = fmtHora.formatToParts(new Date(ms));
        const h = partes.find((p) => p.type === "hour")?.value ?? "";
        const m = partes.find((p) => p.type === "minute")?.value ?? "";
        const am = (partes.find((p) => p.type === "dayPeriod")?.value ?? "").toLowerCase().includes("a") ? "a. m." : "p. m.";
        return { hora: `${h}:${m}`, ampm: am };
    };

    // "45 min", "2 h 10 min", "26 h": legible para cualquiera, sin símbolos.
    const duracion = (ms) => {
        const min = Math.max(0, Math.round(ms / 60_000));
        if (min < 60) { return `${min}\u00a0min`; }
        const h = Math.floor(min / 60);
        const r = min % 60;
        return h >= 24 || r === 0 ? `${h}\u00a0h` : `${h}\u00a0h ${r}\u00a0min`;
    };

    const duracionLarga = (ms) => {
        const min = Math.max(0, Math.ceil(ms / 60_000));
        if (min < 60) { return `${min} min`; }
        const h = Math.floor(min / 60);
        const r = min % 60;
        return r === 0 ? `${h} h` : `${h} h ${r} min`;
    };

    // ------------------------------------------------------------------ vista elegida

    const params = new URLSearchParams(location.search);
    let dia = params.get("dia") || "";
    let bloque = params.get("bloque") || "auto";

    const pantalla = () => contenedor.querySelector("[data-tv-pantalla]");

    const sincronizarUrl = () => {
        const p = new URLSearchParams();
        if (dia) { p.set("dia", dia); }
        if (bloque && bloque !== "auto") { p.set("bloque", bloque); }
        const q = p.toString();
        history.replaceState(null, "", q ? `${location.pathname}?${q}` : location.pathname);
        document.querySelectorAll("[data-tv-bloque]").forEach((b) => {
            b.setAttribute("aria-pressed", b.dataset.tvBloque === bloque ? "true" : "false");
        });
    };

    // ------------------------------------------------------------------ actualización

    const aviso = document.getElementById("tv-desconexion");
    let ultimoHtml = contenedor.innerHTML;
    let ultimaActualizacion = Date.now();
    let peticion = null;
    let secuencia = 0;

    const mostrarDesconexion = (html) => {
        if (!aviso) { return; }
        aviso.innerHTML = html;
        aviso.hidden = false;
    };

    // La actualización de cada minuto no se encima con otra. Un cambio pedido por alguien (otro
    // turno, otro día) sí cancela la que esté en curso: si no, la pantalla podría quedar mostrando
    // la vista anterior con el botón de la nueva marcado.
    async function actualizar(pedidoPorAlguien = false) {
        if (peticion && !pedidoPorAlguien) { return; }
        peticion?.abort();
        const mia = ++secuencia;
        const control = new AbortController();
        peticion = control;
        let vencida = false;
        const limite = setTimeout(() => { vencida = true; control.abort(); }, 45_000);
        if (pedidoPorAlguien) { document.body.classList.add("is-cargando"); }

        const p = new URLSearchParams();
        if (dia) { p.set("dia", dia); }
        p.set("bloque", bloque || "auto");
        try {
            const r = await fetch(`${cfg.panelUrl}?${p}`, { cache: "no-store", credentials: "same-origin", signal: control.signal });
            if (mia !== secuencia) { return; }
            if (r.redirected && /\/Auth\//i.test(r.url)) {
                mostrarDesconexion(`La sesión de Nexa se cerró. <a href="${location.href}">Vuelva a iniciar sesión</a> para seguir viendo los indicadores.`);
                return;
            }
            if (!r.ok) { throw new Error(String(r.status)); }
            const html = await r.text();
            if (mia !== secuencia) { return; }
            const servidor = Number(r.headers.get("X-Servidor-Ms"));
            if (servidor) { desfase = servidor - Date.now(); }
            if (html !== ultimoHtml) {
                contenedor.innerHTML = html;
                ultimoHtml = html;
                prepararTablero();
            }
            ultimaActualizacion = Date.now();
            if (aviso) { aviso.hidden = true; }
        } catch {
            // Cancelada porque alguien pidió otra vista: no es una falla de conexión.
            if (mia !== secuencia || (control.signal.aborted && !vencida)) { return; }
            const { hora, ampm } = partesHora(ultimaActualizacion + desfase);
            mostrarDesconexion(`Sin conexión con Nexa: las cifras son de las ${hora} ${ampm}. Se reintenta cada minuto.`);
        } finally {
            clearTimeout(limite);
            if (mia === secuencia) {
                peticion = null;
                document.body.classList.remove("is-cargando");
            }
        }
    }

    // ------------------------------------------------------------------ tablero: reloj, cinta, listas

    function prepararTablero() {
        if (!pantalla()) { return; }
        medirListas();
        turnarDobles(true);
        pintarSonido();
        latido();
    }

    // Una lista que no cabe se desplaza sola, despacio, ida y vuelta.
    function medirLista(lista) {
        if (!lista) { return; }
        lista.classList.remove("is-desplazando");
        const sobra = lista.scrollHeight - lista.clientHeight;
        if (sobra > 4) {
            lista.style.setProperty("--distancia", `${sobra + 12}px`);
            lista.style.setProperty("--duracion", `${Math.max(14, sobra / 14)}s`);
            lista.classList.add("is-desplazando");
        }
    }

    function medirListas() {
        const raiz = pantalla();
        if (!raiz) { return; }

        raiz.querySelectorAll("[data-tv-desplazar]").forEach(medirLista);

        const marquesina = raiz.querySelector("[data-tv-marquesina]");
        if (marquesina) {
            const pista = marquesina.firstElementChild;
            marquesina.classList.remove("is-desplazando");
            const sobra = (pista?.scrollWidth ?? 0) - marquesina.clientWidth;
            if (sobra > 4) {
                marquesina.style.setProperty("--distancia", `${sobra + 16}px`);
                marquesina.style.setProperty("--duracion", `${Math.max(18, sobra / 40)}s`);
                marquesina.classList.add("is-desplazando");
            }
        }
    }

    // ------------------------------------------------------------------ tarjetas que comparten columna

    // Con más de cuatro personas en Admisiones, dos tarjetas comparten columna. En el modo de un
    // área a la vez no caben las dos listas a ese tamaño de letra: se turnan, una abierta y la otra
    // en una franja. Sigue aunque la rotación esté en pausa; si no, la cerrada no se vería nunca.
    const TURNO_DOBLES_MS = 12000;
    let turnoDobles = -1;

    function turnarDobles(medir) {
        const rotando = document.documentElement.dataset.modo === "rotar";
        const turno = Math.floor(ahora() / TURNO_DOBLES_MS);
        if (!medir && (!rotando || turno === turnoDobles)) { return; }
        turnoDobles = turno;

        pantalla()?.querySelectorAll(".tv-personas__col--doble").forEach((columna) => {
            const tarjetas = [...columna.querySelectorAll(":scope > .tv-persona")];
            const abrir = (abierta) => tarjetas.forEach((t) => t.classList.toggle("is-cerrada", rotando && t !== abierta));
            if (!rotando) {
                abrir(null);
                return;
            }
            // La que no tiene pacientes no pide turno: no tiene lista que mostrar.
            const conPacientes = tarjetas.filter((t) => t.querySelector(".tv-fila"));
            if (medir) {
                // Cada lista se mide con su tarjeta abierta, que es como se va a ver.
                conPacientes.forEach((t) => {
                    abrir(t);
                    medirLista(t.querySelector("[data-tv-desplazar]"));
                });
            }
            abrir(conPacientes.length ? conPacientes[turno % conPacientes.length] : tarjetas[0]);
        });
    }

    // ------------------------------------------------------------------ rotación de áreas

    const riel = document.getElementById("tv-escenas");
    const botonPausa = document.getElementById("tv-pausa");
    let escena = Math.max(0, ESCENAS.indexOf(document.documentElement.dataset.escena));
    let escenaDesde = Date.now();
    // Con ?escena=... se abre en esa área y quieta: sirve para mostrarla o revisarla con calma.
    let pausada = ESCENAS.includes(pedidos.get("escena"));
    let transcurridoAlPausar = 0;

    const duracionEscena = (i) => {
        const segundos = Number(prefs.segundos?.[ESCENAS[i]]) || SEGUNDOS[ESCENAS[i]];
        return 1000 * Math.min(600, Math.max(5, segundos));
    };

    function pintarEscena(reiniciarProgreso) {
        const html = document.documentElement;
        html.dataset.modo = prefs.rotar ? "rotar" : "todo";
        html.dataset.escena = ESCENAS[escena];

        riel?.querySelectorAll("[data-tv-tramo]").forEach((tramo, i) => {
            tramo.style.flexGrow = String(duracionEscena(i) / 1000);
            tramo.style.setProperty("--dur", `${duracionEscena(i)}ms`);
            tramo.classList.toggle("is-hecha", i < escena);
            tramo.classList.toggle("is-activa", i === escena);
            const barra = tramo.firstElementChild;
            if (i === escena && reiniciarProgreso && barra) {
                // Reinicia la animación del tramo aunque sea el mismo que ya estaba activo.
                barra.style.animation = "none";
                void barra.offsetWidth;
                barra.style.animation = "";
            }
        });
        riel?.classList.toggle("is-pausada", pausada);

        document.querySelectorAll("[data-tv-escena]").forEach((b) => {
            b.disabled = !prefs.rotar;
            b.setAttribute("aria-pressed", prefs.rotar && b.dataset.tvEscena === ESCENAS[escena] ? "true" : "false");
        });
        if (botonPausa) {
            botonPausa.disabled = !prefs.rotar;
            botonPausa.setAttribute("aria-pressed", pausada ? "true" : "false");
            botonPausa.querySelector("i").className = pausada ? "bi bi-play-fill" : "bi bi-pause-fill";
            botonPausa.querySelector("span").textContent = pausada ? "Seguir" : "Pausar";
        }
    }

    function irAEscena(i) {
        escena = ((i % ESCENAS.length) + ESCENAS.length) % ESCENAS.length;
        escenaDesde = Date.now();
        transcurridoAlPausar = 0;
        // No se vuelven a medir las listas: las tres áreas conservan su tamaño aunque no se vean,
        // y medirlas reiniciaría el desplazamiento desde el primer paciente en cada vuelta.
        pintarEscena(true);
    }

    function alternarPausa() {
        if (pausada) {
            escenaDesde = Date.now() - transcurridoAlPausar;
        } else {
            transcurridoAlPausar = Date.now() - escenaDesde;
        }
        pausada = !pausada;
        pintarEscena(false);
    }

    function avanzarRotacion() {
        if (prefs.rotar && !pausada && Date.now() - escenaDesde >= duracionEscena(escena)) {
            irAEscena(escena + 1);
        }
    }

    // Los cortes que ya pasaron cuando se abrió la página no cuentan: solo se recarga al cruzar uno.
    let proximaRecarga = ahora();

    function latido() {
        const raiz = pantalla();
        if (!raiz) { return; }
        const n = ahora();
        const d = raiz.dataset;

        const reloj = raiz.querySelector("[data-tv-reloj]");
        if (reloj) {
            const { hora, ampm } = partesHora(n);
            reloj.querySelector(".tv-reloj__hora").textContent = hora;
            reloj.querySelector(".tv-reloj__ampm").textContent = ampm;
        }

        const esHoy = d.esHoy === "true";
        const ini = Number(d.cintaIniMs);
        const fin = Number(d.cintaFinMs);
        const aguja = raiz.querySelector("[data-tv-ahora]");
        const transcurrido = raiz.querySelector("[data-tv-transcurrido]");
        if (aguja && esHoy && fin > ini) {
            const x = Math.min(100, Math.max(0, ((n - ini) / (fin - ini)) * 100));
            aguja.hidden = n < ini || n > fin;
            aguja.style.left = `${x}%`;
            const etiqueta = aguja.querySelector("[data-tv-ahora-hora]");
            if (etiqueta) { etiqueta.textContent = partesHora(n).hora; }
            if (transcurrido) { transcurrido.style.width = `${x}%`; }
        }

        // Cuenta regresiva de la cabecera (solo hoy y en día hábil).
        const cuenta = raiz.querySelector("[data-tv-cuenta]");
        if (cuenta && esHoy && d.habil === "true") {
            const avisoMs = Number(d.avisoMs);
            const corteMs = Number(d.corteMs);
            const entregaMs = Number(d.entregaMs);
            cuenta.classList.toggle("is-pronto", n >= avisoMs && n < entregaMs);
            if (n < corteMs) {
                cuenta.innerHTML = `Corte de turno en <b>${duracionLarga(corteMs - n)}</b>`;
            } else if (n < entregaMs) {
                cuenta.innerHTML = `Entrega de turno en <b>${duracionLarga(entregaMs - n)}</b>`;
            } else {
                cuenta.innerHTML = "Termina a las <b>10:00 p. m.</b>";
            }
        }

        // Tiempos de espera de las analistas, en vivo.
        raiz.querySelectorAll("[data-tv-espera]").forEach((el) => {
            const desde = Number(el.dataset.tvEspera);
            const hasta = Number(el.dataset.tvHasta) || n;
            el.textContent = duracion(hasta - desde);
        });

        // Al corte y a la medianoche se pide el tablero en el acto: cambia el turno o el día.
        const cambios = [Number(d.corteMs) + 5_000, Number(d.finDiaMs) + 5_000].filter((x) => x > 0);
        for (const c of cambios) {
            if (n >= c && proximaRecarga < c) {
                proximaRecarga = c;
                actualizar(true);
            }
        }

        revisarAvisoDeVoz(n);
        avanzarRotacion();
        turnarDobles(false);
    }

    // ------------------------------------------------------------------ sonido

    let contextoAudio = null;
    let sonidoActivo = false;
    const capaActivar = document.getElementById("tv-activar");

    const crearContexto = () => {
        if (!contextoAudio) {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            if (Ctx) { contextoAudio = new Ctx(); }
        }
        return contextoAudio;
    };

    function pintarSonido() {
        const icono = pantalla()?.querySelector("[data-tv-sonido]");
        if (!icono) { return; }
        icono.classList.toggle("is-activo", sonidoActivo);
        icono.title = sonidoActivo ? "Sonido activo: el aviso de la 1:30 p. m. sonará" : "Sonido apagado: toque la pantalla para activarlo";
        icono.querySelector("i").className = sonidoActivo ? "bi bi-volume-up-fill" : "bi bi-volume-mute-fill";
    }

    // El navegador solo deja sonar audio después de un gesto. Con Chrome o Edge en modo quiosco y
    // --autoplay-policy=no-user-gesture-required, el contexto ya nace "running" y no se pregunta.
    async function activarSonido() {
        const ctx = crearContexto();
        try { if (ctx && ctx.state !== "running") { await ctx.resume(); } } catch { /* sin audio web */ }
        if ("speechSynthesis" in window) {
            // Una frase vacía "desbloquea" la voz en Chrome para usos posteriores sin gesto.
            const u = new SpeechSynthesisUtterance(" ");
            u.volume = 0;
            speechSynthesis.speak(u);
        }
        sonidoActivo = !ctx || ctx.state === "running";
        if (capaActivar) { capaActivar.hidden = sonidoActivo; }
        pintarSonido();
        pedirPantallaEncendida();
    }

    function comprobarSonido() {
        const ctx = crearContexto();
        sonidoActivo = !!ctx && ctx.state === "running";
        if (capaActivar) { capaActivar.hidden = sonidoActivo; }
        pintarSonido();
    }

    const esperar = (ms) => new Promise((r) => setTimeout(r, ms));

    // Campana de tres tonos, como la de un aeropuerto. Se genera aquí: no hay archivo de audio.
    async function campana() {
        const ctx = crearContexto();
        if (!ctx) { return; }
        try { if (ctx.state !== "running") { await ctx.resume(); } } catch { return; }
        const inicio = ctx.currentTime + 0.05;
        [[783.99, 0], [659.25, 0.45], [523.25, 0.9]].forEach(([frecuencia, retraso]) => {
            const osc = ctx.createOscillator();
            const gan = ctx.createGain();
            osc.type = "sine";
            osc.frequency.value = frecuencia;
            const t0 = inicio + retraso;
            gan.gain.setValueAtTime(0.0001, t0);
            gan.gain.exponentialRampToValueAtTime(0.5 * prefs.volumen, t0 + 0.02);
            gan.gain.exponentialRampToValueAtTime(0.0001, t0 + 1.4);
            osc.connect(gan).connect(ctx.destination);
            osc.start(t0);
            osc.stop(t0 + 1.5);
        });
        await esperar(2300);
    }

    const ordenIdioma = (lang) => {
        const l = (lang || "").toLowerCase().replace("_", "-");
        if (l === "es-co") { return 0; }
        if (l === "es-mx" || l === "es-419" || l === "es-us") { return 1; }
        if (l.startsWith("es")) { return 2; }
        return 9;
    };
    const esNatural = (v) => /natural|online|neural/i.test(v.name);

    function vocesEnEspanol() {
        if (!("speechSynthesis" in window)) { return []; }
        return speechSynthesis.getVoices()
            .filter((v) => (v.lang || "").toLowerCase().startsWith("es"))
            .sort((a, b) => ordenIdioma(a.lang) - ordenIdioma(b.lang)
                || Number(esNatural(b)) - Number(esNatural(a))
                || a.name.localeCompare(b.name, "es"));
    }

    function vozElegida() {
        const voces = vocesEnEspanol();
        return voces.find((v) => v.voiceURI === prefs.voz) || voces[0] || null;
    }

    function hablar(texto) {
        return new Promise((resolver) => {
            if (!("speechSynthesis" in window)) { resolver(); return; }
            const u = new SpeechSynthesisUtterance(texto);
            const voz = vozElegida();
            if (voz) { u.voice = voz; u.lang = voz.lang; } else { u.lang = "es-CO"; }
            u.volume = prefs.volumen;
            u.rate = prefs.velocidad;
            let listo = false;
            const terminar = () => { if (!listo) { listo = true; resolver(); } };
            u.onend = terminar;
            u.onerror = terminar;
            speechSynthesis.cancel();
            speechSynthesis.speak(u);
            setTimeout(terminar, 25_000);
        });
    }

    const capaAviso = document.getElementById("tv-aviso");
    const textoAviso = document.getElementById("tv-aviso-texto");
    let ocultarAvisoEn = 0;

    async function anunciar(texto) {
        // Si quedó pendiente el ocultamiento de un aviso anterior, no debe tapar este.
        clearTimeout(ocultarAvisoEn);
        if (capaAviso && textoAviso) {
            textoAviso.textContent = texto;
            capaAviso.hidden = false;
        }
        if (prefs.campana) { await campana(); }
        await hablar(texto);
        if (prefs.repetir) {
            await esperar(1500);
            await hablar(texto);
        }
        clearTimeout(ocultarAvisoEn);
        ocultarAvisoEn = setTimeout(() => { if (capaAviso) { capaAviso.hidden = true; } }, 20_000);
    }

    const mensajeElegido = () => (MENSAJES[prefs.mensaje] || MENSAJES[0]).texto;

    let avisoDeHoyEnMemoria = "";

    function revisarAvisoDeVoz(n) {
        const raiz = pantalla();
        // El aviso es del día real, no del que se está mirando: vale aunque la pantalla muestre
        // un día anterior.
        const avisoMs = Number(raiz?.dataset.hoyAvisoMs || 0);
        if (!avisoMs || n < avisoMs || n >= avisoMs + VENTANA_AVISO_MS) { return; }
        const clave = CLAVE_AVISO + raiz.dataset.hoy;
        let yaSono = avisoDeHoyEnMemoria === clave;
        try { yaSono = yaSono || localStorage.getItem(clave) === "1"; } catch { /* solo memoria */ }
        if (yaSono) { return; }
        avisoDeHoyEnMemoria = clave;
        try { localStorage.setItem(clave, "1"); } catch { /* solo memoria */ }
        anunciar(mensajeElegido());
    }

    // ------------------------------------------------------------------ pantalla encendida

    let bloqueoPantalla = null;
    async function pedirPantallaEncendida() {
        try {
            if ("wakeLock" in navigator && !bloqueoPantalla && document.visibilityState === "visible") {
                bloqueoPantalla = await navigator.wakeLock.request("screen");
                bloqueoPantalla.addEventListener("release", () => { bloqueoPantalla = null; });
            }
        } catch { /* el navegador no lo permite: depende de la configuración del televisor */ }
    }

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") {
            pedirPantallaEncendida();
            actualizar();
        }
    });

    // ------------------------------------------------------------------ controles y ajustes

    const controles = document.getElementById("tv-controles");
    const ajustes = document.getElementById("tv-ajustes");
    let ocultarControlesEn = 0;

    let esperasPorFecha = 0;

    function mostrarControles() {
        document.body.classList.remove("is-quieto");
        controles?.classList.add("is-visible");
        clearTimeout(ocultarControlesEn);
        esperasPorFecha = 0;
        ocultarControlesEn = setTimeout(ocultarControles, 6000);
    }

    // Seis segundos sin tocar nada y la barra se va. Antes se quedaba para siempre después de
    // pulsar cualquiera de sus botones: el botón conservaba el foco y con el foco dentro no se
    // ocultaba. Ahora el foco se suelta; solo espera si están abiertos los ajustes o el calendario
    // de la fecha (ocultarla le quitaría el calendario a quien está eligiendo el día), y a ese
    // le da medio minuto como mucho: el campo de fecha también conserva el foco.
    function ocultarControles() {
        const conFoco = controles?.contains(document.activeElement) ? document.activeElement : null;
        const eligiendoFecha = Boolean(conFoco?.matches("input[type=date]")) && esperasPorFecha < 4;
        if ((ajustes && !ajustes.hidden) || eligiendoFecha) {
            if (eligiendoFecha) { esperasPorFecha++; }
            ocultarControlesEn = setTimeout(ocultarControles, 6000);
            return;
        }
        conFoco?.blur();
        controles?.classList.remove("is-visible");
        document.body.classList.add("is-quieto");
    }

    ["mousemove", "pointerdown", "keydown", "touchstart"].forEach((evento) => {
        document.addEventListener(evento, mostrarControles, { passive: true });
    });

    // Cualquier gesto sirve para activar el sonido.
    ["pointerdown", "keydown", "touchstart"].forEach((evento) => {
        document.addEventListener(evento, () => { if (!sonidoActivo) { activarSonido(); } }, { passive: true });
    });

    document.getElementById("tv-activar-boton")?.addEventListener("click", activarSonido);
    contenedor.addEventListener("click", (e) => {
        if (e.target.closest("[data-tv-sonido]")) { abrirAjustes(); }
    });

    document.querySelectorAll("[data-tv-bloque]").forEach((boton) => {
        boton.addEventListener("click", () => {
            bloque = boton.dataset.tvBloque;
            sincronizarUrl();
            actualizar(true);
        });
    });

    document.querySelectorAll("[data-tv-escena]").forEach((boton) => {
        boton.addEventListener("click", () => irAEscena(ESCENAS.indexOf(boton.dataset.tvEscena)));
    });
    botonPausa?.addEventListener("click", alternarPausa);

    const campoFecha = document.getElementById("tv-fecha");
    campoFecha?.addEventListener("change", () => {
        const hoy = pantalla()?.dataset.hoy || "";
        dia = campoFecha.value && campoFecha.value !== hoy ? campoFecha.value : "";
        bloque = dia ? "dia" : "auto";
        sincronizarUrl();
        actualizar(true);
        // Ya eligió el día: se suelta el foco para que la barra pueda ocultarse.
        campoFecha.blur();
    });

    document.getElementById("tv-hoy")?.addEventListener("click", () => {
        dia = "";
        bloque = "auto";
        if (campoFecha) { campoFecha.value = pantalla()?.dataset.hoy || ""; }
        sincronizarUrl();
        actualizar(true);
    });

    document.getElementById("tv-pantalla-completa")?.addEventListener("click", () => {
        if (document.fullscreenElement) { document.exitFullscreen?.(); }
        else { document.documentElement.requestFullscreen?.().catch(() => { /* no permitido */ }); }
    });

    function abrirAjustes() {
        if (!ajustes) { return; }
        ajustes.hidden = false;
        llenarVoces();
        actualizarProximoAviso();
        document.getElementById("tv-cerrar-ajustes")?.focus();
    }

    function cerrarAjustes() {
        if (ajustes) { ajustes.hidden = true; }
        mostrarControles();
    }

    document.getElementById("tv-abrir-ajustes")?.addEventListener("click", abrirAjustes);
    document.getElementById("tv-cerrar-ajustes")?.addEventListener("click", cerrarAjustes);

    document.addEventListener("keydown", (e) => {
        if (e.target.closest("input, select, textarea")) { return; }
        const teclas = { a: "auto", m: "manana", t: "tarde", d: "dia" };
        if (e.key === "Escape") { cerrarAjustes(); return; }
        if (teclas[e.key]) {
            bloque = teclas[e.key];
            sincronizarUrl();
            actualizar(true);
        } else if (e.key === "f") {
            document.getElementById("tv-pantalla-completa")?.click();
        } else if (e.key === "s") {
            abrirAjustes();
        } else if (prefs.rotar && ["1", "2", "3"].includes(e.key)) {
            irAEscena(Number(e.key) - 1);
        } else if (prefs.rotar && e.key === "ArrowRight") {
            irAEscena(escena + 1);
        } else if (prefs.rotar && e.key === "ArrowLeft") {
            irAEscena(escena - 1);
        } else if (prefs.rotar && e.key === " " && !e.target.closest("button, a")) {
            e.preventDefault();
            alternarPausa();
        }
    });

    // Voces
    const selectorVoz = document.getElementById("tv-voz");
    const ayudaVoz = document.getElementById("tv-voz-ayuda");

    function llenarVoces() {
        if (!selectorVoz) { return; }
        const voces = vocesEnEspanol();
        selectorVoz.innerHTML = "";
        voces.forEach((v, i) => {
            const opcion = document.createElement("option");
            opcion.value = v.voiceURI;
            const pais = (v.lang || "").replace("_", "-");
            opcion.textContent = `${v.name} · ${pais}${i === 0 ? " · recomendada" : ""}`;
            selectorVoz.append(opcion);
        });
        const elegida = vozElegida();
        if (elegida) { selectorVoz.value = elegida.voiceURI; }
        selectorVoz.disabled = voces.length === 0;
        if (ayudaVoz) {
            ayudaVoz.textContent = !("speechSynthesis" in window)
                ? "Este navegador no puede hablar: el aviso sonará como campana y se mostrará escrito en pantalla."
                : voces.length === 0
                    ? "Este navegador no trae voces en español: el aviso sonará como campana y se mostrará escrito. Microsoft Edge trae voces naturales de Colombia (Salomé, Gonzalo)."
                    : `${voces.length} voces en español disponibles en este televisor. Las de Colombia y las naturales van primero.`;
        }
    }

    selectorVoz?.addEventListener("change", () => {
        prefs.voz = selectorVoz.value;
        guardarPrefs();
    });

    if ("speechSynthesis" in window) {
        speechSynthesis.addEventListener?.("voiceschanged", () => { if (ajustes && !ajustes.hidden) { llenarVoces(); } });
    }

    // Mensajes
    const listaMensajes = document.getElementById("tv-mensajes");
    MENSAJES.forEach((m, i) => {
        const etiqueta = document.createElement("label");
        etiqueta.className = "tv-mensaje";
        etiqueta.innerHTML = `<input type="radio" name="tv-mensaje" value="${i}"><span><b></b><span></span></span>`;
        etiqueta.querySelector("b").textContent = m.titulo;
        etiqueta.querySelector("span > span").textContent = `“${m.texto}”`;
        const radio = etiqueta.querySelector("input");
        radio.checked = i === Number(prefs.mensaje);
        radio.addEventListener("change", () => { prefs.mensaje = i; guardarPrefs(); });
        listaMensajes?.append(etiqueta);
    });

    const enlazarRango = (id, clave) => {
        const el = document.getElementById(id);
        if (!el) { return; }
        el.value = prefs[clave];
        el.addEventListener("input", () => { prefs[clave] = Number(el.value); guardarPrefs(); });
    };
    enlazarRango("tv-volumen", "volumen");
    enlazarRango("tv-velocidad", "velocidad");

    const enlazarCasilla = (id, clave, alCambiar) => {
        const el = document.getElementById(id);
        if (!el) { return; }
        el.checked = !!prefs[clave];
        el.addEventListener("change", () => { prefs[clave] = el.checked; guardarPrefs(); alCambiar?.(); });
    };
    enlazarCasilla("tv-campana", "campana");
    enlazarCasilla("tv-repetir", "repetir");

    const capaGuias = document.getElementById("tv-guias-capa");
    const aplicarPantalla = () => {
        document.body.dataset.margen = prefs.margen;
        document.documentElement.dataset.tema = prefs.tema;
        if (capaGuias) { capaGuias.hidden = !prefs.guias; }
        pintarEscena(false);
        // El margen y el modo cambian el espacio disponible: las listas vuelven a medirse.
        requestAnimationFrame(prepararTablero);
    };

    enlazarCasilla("tv-rotar", "rotar", () => {
        escenaDesde = Date.now();
        aplicarPantalla();
        pintarEscena(true);
    });
    ESCENAS.forEach((nombre) => {
        const campo = document.getElementById(`tv-seg-${nombre}`);
        if (!campo) { return; }
        campo.value = Math.round(duracionEscena(ESCENAS.indexOf(nombre)) / 1000);
        campo.addEventListener("change", () => {
            const valor = Math.min(600, Math.max(5, Math.round(Number(campo.value)) || SEGUNDOS[nombre]));
            campo.value = valor;
            prefs.segundos = { ...SEGUNDOS, ...prefs.segundos, [nombre]: valor };
            guardarPrefs();
            escenaDesde = Date.now();
            pintarEscena(true);
        });
    });
    enlazarCasilla("tv-guias", "guias", aplicarPantalla);
    document.querySelectorAll("input[name='tv-margen']").forEach((radio) => {
        radio.checked = radio.value === prefs.margen;
        radio.addEventListener("change", () => { prefs.margen = radio.value; guardarPrefs(); aplicarPantalla(); });
    });
    document.querySelectorAll("input[name='tv-tema']").forEach((radio) => {
        radio.checked = radio.value === prefs.tema;
        radio.addEventListener("change", () => { prefs.tema = radio.value; guardarPrefs(); aplicarPantalla(); });
    });

    document.getElementById("tv-probar")?.addEventListener("click", async () => {
        await activarSonido();
        anunciar(mensajeElegido());
    });

    function actualizarProximoAviso() {
        const destino = document.getElementById("tv-proximo-aviso");
        const raiz = pantalla();
        if (!destino || !raiz) { return; }
        const avisoMs = Number(raiz.dataset.hoyAvisoMs || 0);
        const base = "Suena de lunes a viernes a la 1:30 p. m., quince minutos antes del corte; los fines de semana y festivos no hay entrega.";
        destino.textContent = avisoMs && ahora() < avisoMs
            ? `${base} Próximo aviso: hoy a la 1:30 p. m.`
            : `${base} Hoy ya no hay aviso pendiente.`;
    }

    // Turnos de hoy
    const tokenAntifalsificacion = document.querySelector("input[name='__RequestVerificationToken']")?.value || "";
    document.querySelectorAll("[data-tv-turno]").forEach((selector) => {
        selector.addEventListener("change", async () => {
            const datos = new FormData();
            datos.set("dia", pantalla()?.dataset.hoy || "");
            datos.set("personaId", selector.dataset.tvTurno);
            datos.set("turno", selector.value);
            selector.disabled = true;
            try {
                const r = await fetch(cfg.ajustarTurnoUrl, {
                    method: "POST",
                    body: datos,
                    headers: { RequestVerificationToken: tokenAntifalsificacion },
                    credentials: "same-origin"
                });
                if (!r.ok) { throw new Error(String(r.status)); }
                await actualizar(true);
            } catch {
                mostrarDesconexion("No se pudo guardar el turno. Inténtelo de nuevo.");
            } finally {
                selector.disabled = false;
            }
        });
    });

    // ------------------------------------------------------------------ arranque

    aplicarPantalla();
    pintarEscena(true);
    sincronizarUrl();
    comprobarSonido();
    pedirPantallaEncendida();
    prepararTablero();
    mostrarControles();
    setInterval(latido, 1000);
    setInterval(actualizar, INTERVALO_MS);
    window.addEventListener("resize", () => requestAnimationFrame(prepararTablero));
})();
