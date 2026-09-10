// ============================================================================
// Combo con búsqueda y lista cerrada
//
// Mejora progresiva sobre un <select> normal: se escribe para filtrar, pero lo
// que se guarda solo puede salir de la lista.
//
// El <select> no se reemplaza, se conserva oculto y sigue siendo el campo del
// formulario. Por eso lo que viaja al servidor es siempre una de sus opciones:
// no hay forma de mandar texto libre, ni escribiéndolo ni pegándolo. Si el
// script no llega a ejecutarse, queda el <select> de siempre, que también es
// cerrado; nunca se degrada a texto libre.
//
// Nace de un caso real: la IPS que remite era un <input list> con <datalist>,
// que filtra pero acepta cualquier cosa, y por ahí entraron 407 pacientes con
// "URGENCIAS IPS SURA LOS ROBLEDO" y varias decenas con "SURA", "AMBULATORIO"
// o el nombre de la aseguradora. Filtrar y aceptar texto libre no son la misma
// cosa, y el <datalist> las mezclaba.
//
// Uso:  <select asp-for="..." asp-items="..." data-censo-buscable="Buscar IPS...">
// ============================================================================
(() => {
    "use strict";

    const normalizar = (texto) => (texto || "")
        .toString()
        .normalize("NFD")
        // Quita los diacríticos ya separados por NFD, para que "medellin"
        // encuentre "MEDELLÍN". Se escriben por código y no como caracteres
        // literales, que en un rango son invisibles y se pierden al editar.
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase()
        .trim();

    let contador = 0;

    function construir(select) {
        if (select.dataset.censoComboListo === "true") { return; }
        select.dataset.censoComboListo = "true";

        const id = `censo-combo-${++contador}`;
        const placeholder = select.dataset.censoBuscable || "Escribe para buscar";

        // Las opciones se leen una sola vez: el catálogo no cambia mientras la
        // página está abierta. La opción vacía es el "Selecciona..." y no se
        // ofrece como resultado, se representa dejando el campo en blanco.
        const opciones = Array.from(select.options)
            .filter((opcion) => opcion.value !== "")
            .map((opcion) => ({
                valor: opcion.value,
                texto: opcion.textContent.trim(),
                busqueda: normalizar(opcion.textContent)
            }));

        const envoltorio = document.createElement("div");
        envoltorio.className = "censo-combo";

        const campo = document.createElement("input");
        campo.type = "text";
        campo.className = "form-control censo-combo__campo";
        campo.id = id;
        campo.autocomplete = "off";
        campo.placeholder = placeholder;
        campo.setAttribute("role", "combobox");
        campo.setAttribute("aria-expanded", "false");
        campo.setAttribute("aria-autocomplete", "list");
        campo.setAttribute("aria-controls", `${id}-lista`);

        const etiqueta = select.labels && select.labels[0];
        if (etiqueta) { campo.setAttribute("aria-labelledby", etiqueta.id || (etiqueta.id = `${id}-label`)); }

        const lista = document.createElement("ul");
        lista.className = "censo-combo__lista";
        lista.id = `${id}-lista`;
        lista.setAttribute("role", "listbox");
        lista.hidden = true;

        select.classList.add("censo-combo__select");
        select.setAttribute("tabindex", "-1");
        select.setAttribute("aria-hidden", "true");

        select.parentNode.insertBefore(envoltorio, select);
        envoltorio.appendChild(campo);
        envoltorio.appendChild(select);
        envoltorio.appendChild(lista);

        let resaltada = -1;
        let visibles = [];

        const textoDelValor = (valor) => {
            const encontrada = opciones.find((o) => o.valor === valor);
            return encontrada ? encontrada.texto : "";
        };

        // El campo siempre termina mostrando lo que de verdad está elegido. Es
        // lo que impide que quede en pantalla algo que no se guardó.
        const sincronizar = () => { campo.value = textoDelValor(select.value); };

        const cerrar = () => {
            lista.hidden = true;
            campo.setAttribute("aria-expanded", "false");
            campo.removeAttribute("aria-activedescendant");
            resaltada = -1;
        };

        const resaltar = (indice) => {
            Array.from(lista.children).forEach((li, i) => {
                const activa = i === indice;
                li.classList.toggle("is-activa", activa);
                li.setAttribute("aria-selected", activa ? "true" : "false");
                if (activa) {
                    campo.setAttribute("aria-activedescendant", li.id);
                    li.scrollIntoView({ block: "nearest" });
                }
            });
            resaltada = indice;
        };

        const elegir = (valor) => {
            select.value = valor;
            select.dispatchEvent(new Event("change", { bubbles: true }));
            sincronizar();
            cerrar();
        };

        const abrir = (filtro) => {
            const buscado = normalizar(filtro);
            visibles = buscado === ""
                ? opciones
                : opciones.filter((o) => o.busqueda.includes(buscado));

            lista.innerHTML = "";

            if (visibles.length === 0) {
                const vacio = document.createElement("li");
                vacio.className = "censo-combo__vacio";
                vacio.textContent = "Sin coincidencias";
                lista.appendChild(vacio);
            } else {
                visibles.forEach((opcion, i) => {
                    const li = document.createElement("li");
                    li.id = `${id}-opcion-${i}`;
                    li.className = "censo-combo__opcion";
                    li.setAttribute("role", "option");
                    li.setAttribute("aria-selected", "false");
                    li.dataset.valor = opcion.valor;
                    li.textContent = opcion.texto;
                    lista.appendChild(li);
                });
            }

            lista.hidden = false;
            campo.setAttribute("aria-expanded", "true");
            resaltar(visibles.length > 0 ? 0 : -1);
        };

        campo.addEventListener("input", () => abrir(campo.value));
        campo.addEventListener("focus", () => abrir(""));

        campo.addEventListener("keydown", (evento) => {
            if (evento.key === "ArrowDown" || evento.key === "ArrowUp") {
                evento.preventDefault();
                if (lista.hidden) { abrir(campo.value); return; }
                if (visibles.length === 0) { return; }
                const paso = evento.key === "ArrowDown" ? 1 : -1;
                resaltar((resaltada + paso + visibles.length) % visibles.length);
                return;
            }

            if (evento.key === "Enter") {
                if (!lista.hidden && resaltada >= 0 && visibles[resaltada]) {
                    // Solo dentro del combo: fuera, Enter sigue enviando el formulario.
                    evento.preventDefault();
                    elegir(visibles[resaltada].valor);
                }
                return;
            }

            if (evento.key === "Escape") {
                if (!lista.hidden) { evento.stopPropagation(); }
                sincronizar();
                cerrar();
            }
        });

        lista.addEventListener("mousedown", (evento) => {
            // mousedown y no click: el blur del campo llegaría antes y cerraría
            // la lista, y el clic se perdería.
            const opcion = evento.target.closest(".censo-combo__opcion");
            if (!opcion) { return; }
            evento.preventDefault();
            elegir(opcion.dataset.valor);
        });

        // Al salir se descarta lo escrito y se vuelve a lo elegido. Escribir a
        // medias no deja nada guardado.
        campo.addEventListener("blur", () => {
            window.setTimeout(() => {
                if (!envoltorio.contains(document.activeElement)) {
                    sincronizar();
                    cerrar();
                }
            }, 0);
        });

        // Si algo cambia el <select> por fuera —el rearmado tras un error de
        // validación, por ejemplo— el campo se pone al día.
        select.addEventListener("change", sincronizar);

        sincronizar();
    }

    const iniciar = () =>
        document.querySelectorAll("select[data-censo-buscable]").forEach(construir);

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", iniciar);
    } else {
        iniciar();
    }

    // Los formularios de programa se pintan al vuelo cuando se cambia de
    // atención, así que se expone para volver a recorrer lo nuevo.
    window.censoComboBuscable = iniciar;
})();
