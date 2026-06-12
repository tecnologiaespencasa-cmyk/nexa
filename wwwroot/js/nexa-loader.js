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
    if (el) el.classList.add('hidden');
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
