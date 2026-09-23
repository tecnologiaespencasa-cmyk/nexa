/* ============================================================
   Hoja de vida del paciente
   Todo el contenido llega armado desde el servidor; este script solo agrega
   navegación y lectura: ir de la línea de vida a un ingreso, globos de ayuda,
   filtro de novedades, abrir o cerrar todos los ingresos, marcar la sección
   visible en el índice y desplegar todo al imprimir.
   ============================================================ */
(function () {
  'use strict';

  var hoja = document.querySelector('.hv--hoja');
  if (!hoja) return;

  var reducirMovimiento = window.matchMedia
    ? window.matchMedia('(prefers-reduced-motion: reduce)').matches
    : false;

  /* ---- Ir a un ingreso, una novedad o una ronda ---- */

  function destacar(elemento) {
    elemento.classList.remove('hv-resaltado');
    // Forzar un reflujo para que la transición se repita si se pulsa dos veces.
    void elemento.offsetWidth;
    elemento.classList.add('hv-resaltado');
    window.setTimeout(function () { elemento.classList.remove('hv-resaltado'); }, 1800);
  }

  function irA(id) {
    var destino = document.getElementById(id);
    if (!destino) return;

    if (destino.hidden) {
      aplicarFiltro('todas');
    }

    var pliegue = destino.querySelector('details');
    if (pliegue && !pliegue.open) pliegue.open = true;

    destino.scrollIntoView({ behavior: reducirMovimiento ? 'auto' : 'smooth', block: 'start' });
    destacar(destino);

    var foco = destino.querySelector('summary');
    if (!foco) {
      if (!destino.hasAttribute('tabindex')) destino.setAttribute('tabindex', '-1');
      foco = destino;
    }
    foco.focus({ preventScroll: true });
  }

  document.addEventListener('click', function (evento) {
    var enlace = evento.target.closest('[data-hv-ir]');
    if (!enlace || !hoja.contains(enlace)) return;
    evento.preventDefault();
    ocultarTip();
    irA(enlace.getAttribute('data-hv-ir'));
  });

  /* ---- Globo de ayuda de la línea de vida ---- */

  var tip = document.getElementById('hvTip');
  var tipDueno = null;

  function mostrarTip(elemento) {
    if (!tip) return;
    var texto = elemento.getAttribute('data-hv-tip');
    if (!texto) return;

    tipDueno = elemento;
    tip.textContent = texto;
    tip.hidden = false;

    var caja = elemento.getBoundingClientRect();
    var ancho = tip.offsetWidth;
    var alto = tip.offsetHeight;
    var margen = 8;
    var x = caja.left + caja.width / 2 - ancho / 2;
    x = Math.max(margen, Math.min(x, window.innerWidth - ancho - margen));
    var y = caja.top - alto - 10;
    if (y < margen) y = caja.bottom + 10;

    tip.style.left = Math.round(x) + 'px';
    tip.style.top = Math.round(y) + 'px';
  }

  function ocultarTip() {
    if (!tip) return;
    tip.hidden = true;
    tipDueno = null;
  }

  hoja.addEventListener('pointerover', function (evento) {
    var objetivo = evento.target.closest('[data-hv-tip]');
    if (objetivo) mostrarTip(objetivo);
  });

  hoja.addEventListener('pointerout', function (evento) {
    var objetivo = evento.target.closest('[data-hv-tip]');
    if (objetivo && objetivo === tipDueno && !objetivo.contains(evento.relatedTarget)) ocultarTip();
  });

  hoja.addEventListener('focusin', function (evento) {
    var objetivo = evento.target.closest('[data-hv-tip]');
    if (objetivo) mostrarTip(objetivo);
  });

  hoja.addEventListener('focusout', function (evento) {
    if (evento.target === tipDueno) ocultarTip();
  });

  document.addEventListener('keydown', function (evento) {
    if (evento.key === 'Escape') ocultarTip();
  });

  window.addEventListener('scroll', ocultarTip, { passive: true });
  var papel = hoja.querySelector('.hv-ecg');
  if (papel) {
    // En pantallas angostas el papel se desplaza de lado: se abre mostrando el presente, que es
    // lo primero que se busca, en vez del ingreso más antiguo. Se espera al siguiente cuadro
    // porque antes del primer dibujo el ancho real todavía no está calculado.
    requestAnimationFrame(function () {
      requestAnimationFrame(function () {
        if (papel.scrollWidth > papel.clientWidth) papel.scrollLeft = papel.scrollWidth;
      });
    });
    papel.addEventListener('scroll', ocultarTip, { passive: true });
  }

  /* ---- Filtro de novedades ---- */

  var botonesFiltro = hoja.querySelectorAll('[data-hv-filtro]');

  function aplicarFiltro(valor) {
    botonesFiltro.forEach(function (boton) {
      boton.setAttribute('aria-pressed', boton.getAttribute('data-hv-filtro') === valor ? 'true' : 'false');
    });
    hoja.querySelectorAll('.hv-novedad[data-hv-pendiente]').forEach(function (novedad) {
      novedad.hidden = valor === 'pendientes' && novedad.getAttribute('data-hv-pendiente') !== 'true';
    });
  }

  botonesFiltro.forEach(function (boton) {
    boton.addEventListener('click', function () {
      aplicarFiltro(boton.getAttribute('data-hv-filtro'));
    });
  });

  /* ---- Abrir o cerrar todos los ingresos ---- */

  var botonPlegar = hoja.querySelector('[data-hv-plegar]');
  var pliegues = hoja.querySelectorAll('.hv-ingreso__pliegue');

  function actualizarBotonPlegar() {
    if (!botonPlegar) return;
    var todosAbiertos = Array.prototype.every.call(pliegues, function (p) { return p.open; });
    botonPlegar.setAttribute('aria-pressed', todosAbiertos ? 'true' : 'false');
    botonPlegar.querySelector('span').textContent = todosAbiertos ? 'Cerrar todos' : 'Abrir todos';
    botonPlegar.querySelector('i').className = 'bi ' + (todosAbiertos ? 'bi-arrows-collapse' : 'bi-arrows-expand');
  }

  if (botonPlegar) {
    botonPlegar.addEventListener('click', function () {
      var abrir = botonPlegar.getAttribute('aria-pressed') !== 'true';
      pliegues.forEach(function (p) { p.open = abrir; });
      actualizarBotonPlegar();
    });
    pliegues.forEach(function (p) { p.addEventListener('toggle', actualizarBotonPlegar); });
    actualizarBotonPlegar();
  }

  /* ---- Índice: marcar la sección que se está leyendo ---- */

  var enlacesIndice = hoja.querySelectorAll('.hv-indice a[href^="#"]');
  if ('IntersectionObserver' in window && enlacesIndice.length) {
    var porId = {};
    enlacesIndice.forEach(function (enlace) {
      porId[enlace.getAttribute('href').slice(1)] = enlace;
    });

    var visibles = {};
    var observador = new IntersectionObserver(function (entradas) {
      entradas.forEach(function (entrada) {
        visibles[entrada.target.id] = entrada.isIntersecting;
      });
      var actual = null;
      enlacesIndice.forEach(function (enlace) {
        var id = enlace.getAttribute('href').slice(1);
        if (!actual && visibles[id]) actual = id;
      });
      enlacesIndice.forEach(function (enlace) {
        if (enlace.getAttribute('href').slice(1) === actual) {
          if (enlace.getAttribute('aria-current') !== 'true') {
            // En el celular el índice se desplaza de lado: la sección activa no debe quedar cortada.
            var indice = enlace.parentElement;
            indice.scrollTo({
              left: enlace.offsetLeft - (indice.clientWidth - enlace.offsetWidth) / 2,
              behavior: reducirMovimiento ? 'auto' : 'smooth'
            });
          }
          enlace.setAttribute('aria-current', 'true');
        } else {
          enlace.removeAttribute('aria-current');
        }
      });
    }, { rootMargin: '-15% 0px -55% 0px' });

    Object.keys(porId).forEach(function (id) {
      var seccion = document.getElementById(id);
      if (seccion) observador.observe(seccion);
    });
  }

  /* ---- Barras y trazos que crecen al asomarse ----
     La clase hv-anima-listo es la que pone los valores en cero para animarlos: sin ella —sin
     JavaScript, o con movimiento reducido— cada barra se dibuja directamente en su tamaño real. */

  if ('IntersectionObserver' in window && !reducirMovimiento) {
    hoja.classList.add('hv-anima-listo');

    var crecer = new IntersectionObserver(function (entradas) {
      entradas.forEach(function (entrada) {
        if (!entrada.isIntersecting) return;
        entrada.target.classList.add('hv-visible');
        crecer.unobserve(entrada.target);
      });
    }, { rootMargin: '0px 0px -10% 0px', threshold: 0.25 });

    hoja.querySelectorAll('.hv-escala, .hv-frecuencias li, .hv-evolucion').forEach(function (pieza) {
      crecer.observe(pieza);
    });

    // Lo que ya está desplegado dentro de un ingreso abierto se observa al abrirlo.
    hoja.addEventListener('toggle', function (evento) {
      if (!evento.target.open) return;
      evento.target.querySelectorAll('.hv-escala:not(.hv-visible), .hv-evolucion:not(.hv-visible)').forEach(function (pieza) {
        crecer.observe(pieza);
      });
    }, true);
  }

  /* ---- Imprimir con todos los ingresos desplegados ---- */

  var abiertosAntesDeImprimir = [];
  window.addEventListener('beforeprint', function () {
    abiertosAntesDeImprimir = Array.prototype.map.call(pliegues, function (p) { return p.open; });
    pliegues.forEach(function (p) { p.open = true; });
    aplicarFiltro('todas');
  });
  window.addEventListener('afterprint', function () {
    pliegues.forEach(function (p, i) { p.open = !!abiertosAntesDeImprimir[i]; });
    actualizarBotonPlegar();
  });
})();
