// Reportes: las fichas del "Censo de hoy" funcionan como pestañas y los gráficos muestran el
// rango exacto de cada barra al señalarla. Sin este archivo la página sigue funcionando: cada
// ficha es un enlace que recarga la página con su panel abierto.
(function () {
    "use strict";

    var raiz = document.querySelector("[data-rp]");
    if (!raiz) {
        return;
    }

    // ------------------------------------------------------------------------------------------
    // Pestañas
    // ------------------------------------------------------------------------------------------

    var fichas = Array.prototype.slice.call(raiz.querySelectorAll('[role="tab"][data-rp-vista]'));

    function actualizarVistaEnEnlaces(vista) {
        // Los filtros, los periodos rápidos y el tabulado vuelven a esta misma pestaña.
        raiz.querySelectorAll("[data-rp-vista-input]").forEach(function (input) {
            input.value = vista;
        });

        raiz.querySelectorAll("a[href]").forEach(function (enlace) {
            if (enlace.hasAttribute("data-rp-vista")) {
                return;
            }

            var url;
            try {
                url = new URL(enlace.getAttribute("href"), window.location.href);
            } catch (e) {
                return;
            }

            if (url.origin !== window.location.origin || !/\/Reportes(\/Index)?\/?$/i.test(url.pathname)) {
                return;
            }

            url.searchParams.set("Vista", vista);
            enlace.setAttribute("href", url.pathname + url.search + url.hash);
        });

        raiz.querySelectorAll('input[type="hidden"][name="Vista"]').forEach(function (input) {
            input.value = vista;
        });

        try {
            var actual = new URL(window.location.href);
            actual.searchParams.set("Vista", vista);
            window.history.replaceState(null, "", actual.pathname + actual.search + actual.hash);
        } catch (e) {
            // Sin historial disponible la pestaña igual cambia; solo no queda en la dirección.
        }
    }

    function activar(ficha, mover) {
        fichas.forEach(function (otra) {
            var esta = otra === ficha;
            otra.classList.toggle("is-activa", esta);
            otra.setAttribute("aria-selected", esta ? "true" : "false");
            otra.setAttribute("tabindex", esta ? "0" : "-1");

            var panel = document.getElementById(otra.getAttribute("aria-controls"));
            if (panel) {
                panel.hidden = !esta;
            }
        });

        actualizarVistaEnEnlaces(ficha.getAttribute("data-rp-vista"));

        if (mover) {
            ficha.focus();
        }
    }

    fichas.forEach(function (ficha, indice) {
        ficha.addEventListener("click", function (evento) {
            // Ctrl/Cmd+clic o clic central: abrir en otra pestaña del navegador, como cualquier enlace.
            if (evento.ctrlKey || evento.metaKey || evento.shiftKey || evento.button !== 0) {
                return;
            }

            evento.preventDefault();
            activar(ficha, false);
        });

        ficha.addEventListener("keydown", function (evento) {
            var destino = null;
            if (evento.key === "ArrowRight") {
                destino = fichas[(indice + 1) % fichas.length];
            } else if (evento.key === "ArrowLeft") {
                destino = fichas[(indice - 1 + fichas.length) % fichas.length];
            } else if (evento.key === "Home") {
                destino = fichas[0];
            } else if (evento.key === "End") {
                destino = fichas[fichas.length - 1];
            } else if (evento.key === " ") {
                evento.preventDefault();
                activar(ficha, false);
                return;
            }

            if (destino) {
                evento.preventDefault();
                activar(destino, true);
            }
        });
    });

    // ------------------------------------------------------------------------------------------
    // Rótulo emergente: rango exacto de cada barra. Solo agrega; el valor ya está escrito.
    // ------------------------------------------------------------------------------------------

    var caja = raiz.querySelector("[data-rp-tip-caja]");
    if (!caja) {
        return;
    }

    var actualTip = null;

    function ubicar(x, y) {
        var margen = 14;
        var ancho = caja.offsetWidth;
        var alto = caja.offsetHeight;
        var izquierda = Math.min(window.innerWidth - ancho - 8, Math.max(8, x - ancho / 2));
        var arriba = y - alto - margen;
        if (arriba < 8) {
            arriba = y + margen + 8;
        }
        caja.style.transform = "translate(" + Math.round(izquierda) + "px," + Math.round(arriba) + "px)";
    }

    function mostrar(elemento, x, y) {
        var texto = elemento.getAttribute("data-rp-tip");
        if (!texto) {
            return;
        }
        actualTip = elemento;
        caja.textContent = texto;
        caja.hidden = false;
        ubicar(x, y);
    }

    function ocultar() {
        actualTip = null;
        caja.hidden = true;
    }

    raiz.addEventListener("pointermove", function (evento) {
        if (evento.pointerType === "touch") {
            return;
        }
        var elemento = evento.target.closest ? evento.target.closest("[data-rp-tip]") : null;
        if (!elemento || !raiz.contains(elemento)) {
            if (actualTip) {
                ocultar();
            }
            return;
        }
        if (elemento !== actualTip) {
            mostrar(elemento, evento.clientX, evento.clientY);
        } else {
            ubicar(evento.clientX, evento.clientY);
        }
    });

    raiz.addEventListener("pointerleave", ocultar);
    window.addEventListener("scroll", ocultar, { passive: true });
    document.addEventListener("keydown", function (evento) {
        if (evento.key === "Escape") {
            ocultar();
        }
    });
})();
