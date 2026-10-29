/* ============================================================
   NexaLoader — loading overlay global
   API pública: NexaLoader.show() / NexaLoader.hide() / NexaLoader.forceHide()
   ============================================================ */
(function () {
  'use strict';

  /* ---- Estado interno ---- */
  var _count       = 0;   // peticiones activas
  var _showTimer   = null; // setTimeout de los 200 ms de retraso
  var _wmarkTimer  = null; // setInterval del wordmark loop
  var _overlay     = null;

  var _reducedMotion = window.matchMedia
    ? window.matchMedia('(prefers-reduced-motion: reduce)').matches
    : false;

  function _getOverlay() {
    if (!_overlay) _overlay = document.getElementById('nexaLoader');
    return _overlay;
  }

  /* Reinicia la animación letra a letra cada ciclo de 1.8 s */
  function _startWordmarkLoop(root) {
    var els    = root.querySelectorAll('.nexa-wN, .nexa-wexa span');
    var delays = [0, 0.18, 0.32, 0.46];

    function restart() {
      els.forEach(function (el) {
        el.style.animation  = 'none';
        el.style.opacity    = '0';
        el.style.transform  = 'translateY(10px)';
      });
      requestAnimationFrame(function () {
        requestAnimationFrame(function () {
          els.forEach(function (el, i) {
            el.style.animation      = 'nexa-fadeUp .35s ease forwards';
            el.style.animationDelay = delays[i] + 's';
          });
        });
      });
    }

    restart();
    return setInterval(restart, 1800);
  }

  /* Muestra físicamente el overlay (llamado tras el delay) */
  function _doShow() {
    _showTimer = null;
    if (_count <= 0) return;
    var el = _getOverlay();
    if (!el || !el.classList.contains('hidden')) return;
    el.classList.remove('hidden');
    if (!_reducedMotion && _wmarkTimer === null) {
      _wmarkTimer = _startWordmarkLoop(el);
    }
  }

  /* Oculta físicamente el overlay */
  function _doHide() {
    if (_wmarkTimer !== null) {
      clearInterval(_wmarkTimer);
      _wmarkTimer = null;
    }
    var el = _getOverlay();
    /* Mientras dura la carga inicial de una página que la pide (.nexa-carga-inicial), la destapa
       solo el layout cuando terminan sus scripts: un fetch que acabe antes no puede quitarla. */
    if (el && !el.classList.contains('nexa-carga-inicial')) el.classList.add('hidden');
  }

  /* ---- API pública ---- */
  var NexaLoader = {

    /* Incrementa el contador; programa mostrar el overlay tras 200 ms */
    show: function () {
      _count++;
      if (_count === 1 && _showTimer === null) {
        _showTimer = setTimeout(_doShow, 200);
      }
    },

    /* Decrementa el contador; oculta el overlay cuando llega a 0 */
    hide: function () {
      _count = Math.max(0, _count - 1);
      if (_count === 0) {
        if (_showTimer !== null) {
          clearTimeout(_showTimer);
          _showTimer = null;
        }
        _doHide();
      }
    },

    /* True mientras haya una petición o navegación en curso */
    ocupado: function () {
      return _count > 0;
    },

    /* Cierre forzoso (p.ej. en navegación de vuelta con bfcache) */
    forceHide: function () {
      _count = 0;
      if (_showTimer !== null) {
        clearTimeout(_showTimer);
        _showTimer = null;
      }
      _doHide();
    }
  };

  window.NexaLoader = NexaLoader;

  /* ============================================================
     Interceptor jQuery AJAX (ajaxSend / ajaxComplete)
     Los scripts de la aplicación cargan jQuery antes que este
     archivo, así que $ ya está disponible aquí.
     ============================================================ */
  if (typeof jQuery !== 'undefined') {
    jQuery(document)
      .on('ajaxSend',     function () { NexaLoader.show(); })
      .on('ajaxComplete', function () { NexaLoader.hide(); });
  }

  /* ============================================================
     Interceptor fetch()
     ============================================================ */
  var _originalFetch = window.fetch;
  if (typeof _originalFetch === 'function') {
    window.fetch = function () {
      NexaLoader.show();
      return _originalFetch.apply(this, arguments).finally(function () {
        NexaLoader.hide();
      });
    };
  }

  /* ============================================================
     Descargas: enlaces con "download" y formularios data-descarga
     ------------------------------------------------------------
     El navegador baja estos archivos sin avisar nada mientras el servidor los arma, y un
     exportable grande tarda: la gente volvía a hacer clic (y pedía el archivo dos o tres veces).
     Si el servidor fallaba, solo quedaba un aviso en la barra de descargas que nadie veía.

     Ahora el archivo se pide con fetch: el cargador dice "Preparando el archivo" mientras el
     servidor lo arma y se cierra cuando el archivo llega, que es cuando el navegador empieza a
     guardarlo. Mientras tanto, otro clic no pide otra copia. Si algo falla, se cierra y lo dice;
     si la sesión venció, lleva al inicio de sesión. Nunca queda girando: hay un tope de tiempo.
     ============================================================ */
  /* En producción el exportable más pesado (agudos) tarda segundos; desde un equipo con conexión
     lenta a la base puede tardar varios minutos. El tope solo evita que el cargador quede girando
     si la conexión se cuelga: no debe cortar una descarga lenta pero viva. */
  var TOPE_DESCARGA_MS = 10 * 60 * 1000;
  var _descargaEnCurso = false;

  function _nombreDelArchivo(respuesta, respaldo) {
    var disposicion = respuesta.headers.get('Content-Disposition') || '';
    var utf8 = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(disposicion);
    if (utf8) {
      try { return decodeURIComponent(utf8[1].trim().replace(/^"|"$/g, '')); } catch (_) { /* sigue */ }
    }
    var simple = /filename\s*=\s*"?([^";]+)"?/i.exec(disposicion);
    if (simple) return simple[1].trim();
    return respaldo || 'descarga';
  }

  function _avisar(texto) {
    var anterior = document.getElementById('nexaAvisoDescarga');
    if (anterior) anterior.remove();

    var aviso = document.createElement('div');
    aviso.id = 'nexaAvisoDescarga';
    aviso.className = 'nexa-aviso';
    aviso.setAttribute('role', 'alert');

    var mensaje = document.createElement('span');
    mensaje.textContent = texto;
    var cerrar = document.createElement('button');
    cerrar.type = 'button';
    cerrar.className = 'nexa-aviso__cerrar';
    cerrar.setAttribute('aria-label', 'Cerrar aviso');
    cerrar.textContent = '×';
    cerrar.addEventListener('click', function () { aviso.remove(); });

    /* Queda hasta que lo cierren: un exportable lento puede fallar cuando la persona ya miraba
       otra cosa, y un aviso que se borra solo nadie lo ve. */
    aviso.appendChild(mensaje);
    aviso.appendChild(cerrar);
    document.body.appendChild(aviso);
  }

  function _etiquetaDelCargador(texto) {
    var el = _getOverlay();
    if (!el) return null;
    var etiqueta = el.querySelector('.nexa-loading-lbl');
    var antes = { texto: etiqueta ? etiqueta.textContent : null, aria: el.getAttribute('aria-label') };
    if (etiqueta) etiqueta.textContent = texto;
    el.setAttribute('aria-label', texto);
    return function () {
      if (etiqueta && antes.texto !== null) etiqueta.textContent = antes.texto;
      if (antes.aria !== null) el.setAttribute('aria-label', antes.aria);
    };
  }

  function _guardar(blob, nombre) {
    var direccion = URL.createObjectURL(blob);
    var enlace = document.createElement('a');
    enlace.href = direccion;
    enlace.download = nombre;
    enlace.style.display = 'none';
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    /* Se libera después: algunos navegadores todavía están leyendo el blob tras el clic. */
    setTimeout(function () { URL.revokeObjectURL(direccion); }, 60000);
  }

  function _descargar(url, opciones, nombreRespaldo) {
    if (_descargaEnCurso) return;
    _descargaEnCurso = true;

    var restaurarEtiqueta = _etiquetaDelCargador('Preparando el archivo');
    NexaLoader.show();

    var control = typeof AbortController === 'function' ? new AbortController() : null;
    var tope = control ? setTimeout(function () { control.abort(); }, TOPE_DESCARGA_MS) : null;
    var pedido = {};
    for (var clave in opciones) pedido[clave] = opciones[clave];
    pedido.credentials = 'same-origin';
    if (control) pedido.signal = control.signal;

    _originalFetch.call(window, url, pedido)
      .then(function (respuesta) {
        var tipo = (respuesta.headers.get('Content-Type') || '').toLowerCase();
        var esPagina = tipo.indexOf('text/html') === 0;

        /* Sesión vencida: el servidor redirige al inicio de sesión y llega una página. */
        if (esPagina && respuesta.redirected) {
          window.location.assign(respuesta.url);
          return null;
        }

        if (!respuesta.ok || esPagina) {
          throw new Error('respuesta ' + respuesta.status);
        }

        return respuesta.blob().then(function (blob) {
          return { blob: blob, nombre: _nombreDelArchivo(respuesta, nombreRespaldo) };
        });
      })
      .then(function (archivo) {
        if (archivo) _guardar(archivo.blob, archivo.nombre);
      })
      .catch(function (error) {
        _avisar(error && error.name === 'AbortError'
          ? 'El archivo tardó demasiado en generarse y se canceló. Intenta de nuevo en unos minutos.'
          : 'No se pudo generar el archivo. Intenta de nuevo; si sigue fallando, avisa a Tecnología.');
      })
      .finally(function () {
        if (tope !== null) clearTimeout(tope);
        _descargaEnCurso = false;
        if (restaurarEtiqueta) restaurarEtiqueta();
        NexaLoader.hide();
      });
  }

  /* Un envío baja un archivo si lo dice el formulario o el botón que lo envió: un formulario de
     filtros puede tener "Filtrar" (navega) y "Exportar a Excel" (descarga). */
  function _esEnvioDeDescarga(form, boton) {
    return form.hasAttribute('data-descarga') || !!(boton && boton.hasAttribute('data-descarga'));
  }

  /* Solo archivos de esta misma aplicación: un enlace a otro sitio, a un blob o a un data: lo
     sigue manejando el navegador (y el que arma _guardar es un blob, así que no se intercepta). */
  function _esDescargaPropia(url) {
    return (url.protocol === 'http:' || url.protocol === 'https:') && url.origin === window.location.origin;
  }

  if (typeof _originalFetch === 'function' && typeof URL === 'function' && typeof Blob === 'function') {
    document.addEventListener('click', function (e) {
      if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
      var link = e.target.closest('a[download][href]');
      if (!link || link.target === '_blank') return;

      var url;
      try { url = new URL(link.getAttribute('href'), window.location.href); } catch (_) { return; }
      if (!_esDescargaPropia(url)) return;

      e.preventDefault();
      _descargar(url.href, { method: 'GET' }, link.getAttribute('download') || '');
    });

    document.addEventListener('submit', function (e) {
      if (e.defaultPrevented) return;
      var form = e.target;
      if (!form || form.tagName !== 'FORM' || !_esEnvioDeDescarga(form, e.submitter)) return;

      /* Un botón con formaction/formmethod manda sobre el formulario (p. ej. "Exportar a Excel"
         dentro del formulario de filtros de Activos de TI). */
      var boton = e.submitter || null;
      var accion = (boton && boton.getAttribute('formaction')) || form.getAttribute('action') || window.location.href;
      var url;
      try { url = new URL(accion, window.location.href); } catch (_) { return; }
      if (!_esDescargaPropia(url)) return;

      e.preventDefault();
      var datos = new FormData(form);
      if (boton && boton.name) datos.append(boton.name, boton.value);

      var metodo = ((boton && boton.getAttribute('formmethod')) || form.getAttribute('method') || 'GET').toUpperCase();
      if (metodo === 'GET') {
        url.search = new URLSearchParams(datos).toString();
        _descargar(url.href, { method: 'GET' }, '');
      } else {
        _descargar(url.href, { method: metodo, body: datos }, '');
      }
    });
  }

  /* ============================================================
     Navegación de página completa — links internos
     (no AJAX; la nueva página cargará con el overlay ya oculto)
     ============================================================ */
  document.addEventListener('click', function (e) {
    /* Ignorar si alguna tecla modificadora está presionada */
    if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;

    var link = e.target.closest('a[href]');
    if (!link) return;

    var href = link.getAttribute('href');
    if (!href) return;
    /* Ignorar anclas, pseudo-links y nuevas pestañas */
    if (href.charAt(0) === '#') return;
    if (href.indexOf('javascript:') === 0) return;
    if (link.target === '_blank' || link.hasAttribute('download')) return;

    /* Solo misma origen */
    try {
      var url = new URL(href, window.location.href);
      if (url.hostname !== window.location.hostname) return;
      /* Ignorar si es la misma URL (sólo hash o query idéntica) */
      if (url.pathname + url.search === window.location.pathname + window.location.search) return;
    } catch (_) { return; }

    NexaLoader.show();
  });

  /* ============================================================
     Navegación de página completa — formularios normales (no AJAX)
     ============================================================ */
  document.addEventListener('submit', function (e) {
    if (e.defaultPrevented) return;
    var form = e.target;
    if (!form || form.tagName !== 'FORM') return;
    /* Saltar formularios marcados como AJAX (ASP.NET unobtrusive AJAX) */
    if (form.getAttribute('data-ajax') === 'true') return;
    /* Los envíos que bajan un archivo los atiende la sección "Descargas" de arriba: aquí el
       cargador quedaba girando para siempre, porque la página no navega. */
    if (_esEnvioDeDescarga(form, e.submitter)) return;
    NexaLoader.show();
  });

  /* ============================================================
     Restauración bfcache (navegación atrás/adelante)
     Garantiza que el overlay quede oculto si la página vuelve
     del caché del navegador.
     ============================================================ */
  window.addEventListener('pageshow', function (e) {
    if (e.persisted) NexaLoader.forceHide();
  });

})();
