// Script de NPT en la pantalla única del censo.
//
// Vivía en línea en Views/Censo/_NptScripts.cshtml y se sacó a este archivo (2026-09-24) para que el navegador lo
// guarde en caché: en línea se descargaba y se volvía a analizar en cada consulta de un paciente.
// Los valores que dependen del servidor (URLs, permisos, datos del registro) los deja esa vista en
// window.NexaCenso.npt justo antes de cargar este archivo.
(() => {
    const buscarDiagnosticoCie10Url = NexaCenso.npt.buscarDiagnosticoCie10Url;
    const form = document.getElementById("censoNptForm");
    const tipoIdentificacionSelect = document.getElementById("tipoIdentificacionSelect");
    const numeroIdentificacionInput = document.getElementById("numeroIdentificacionInput");
    const cedulaNptFiltroInput = document.getElementById("cedulaNptFiltroInput");
    const cambiarNptPacienteDocumentoInput = document.getElementById("cambiarNptPacienteDocumento");
    const fechaNacimientoInput = document.getElementById("fechaNacimientoInput");
    const edadInput = document.getElementById("edadInput");
    const nombrePacienteInput = document.getElementById("NombrePaciente");
    const auxiliarEnfermeriaInput = document.getElementById("nptAuxiliarEnfermeriaInput");
    const telefonoAdicional2Toggle = document.getElementById("telefonoAdicional2Toggle");
    const telefonoAdicional2Container = document.getElementById("telefonoAdicional2Container");
    const telefonoAdicional2Input = document.getElementById("TelefonoAdicional2");
    const fechaInicioNptInput = document.getElementById("FechaInicioNpt");
    const fechaFinNptInput = document.getElementById("FechaFinNpt");
    const diasTratamientoInput = document.getElementById("DiasTratamiento");
    const cie10Input = document.getElementById("nptCodigoCie10Input");
    const diagnosticoInput = document.getElementById("nptDiagnosticoInput");
    const auxiliarEnfermeriaOptions = Array.from(document.querySelectorAll("#nptAuxiliarEnfermeriaOptions option"))
        .map((option) => option.value || "")
        .filter((value) => value.trim());

    // ----- CIE-10 -----
    // Misma lógica que el maestro (Datos básicos del paciente): el endpoint responde
    // { found, codigo, diagnostico } contra el catálogo general, y el diagnóstico es de
    // solo lectura porque lo dicta el catálogo, no quien diligencia el formulario. NPT no
    // usa el listado cerrado de clínica de heridas: la atención puede corresponder a
    // cualquier diagnóstico, no solo a los de manejo de herida.
    cie10Input?.addEventListener("blur", async () => {
        const codigo = (cie10Input.value || "").trim().toUpperCase();
        cie10Input.value = codigo;
        if (!diagnosticoInput) return;
        if (!/^[A-Z][0-9]{3}$/.test(codigo)) {
            diagnosticoInput.value = "";
            return;
        }
        try {
            const respuesta = await fetch(`${buscarDiagnosticoCie10Url}?codigo=${encodeURIComponent(codigo)}`);
            if (!respuesta.ok) return;
            const datos = await respuesta.json();
            diagnosticoInput.value = datos?.found ? datos.diagnostico : "";
            if (datos?.found) {
                cie10Input.setCustomValidity("");
            } else {
                cie10Input.setCustomValidity("Ese código CIE10 no está en el catálogo.");
                cie10Input.reportValidity();
            }
        } catch { /* sin conexión: el usuario puede continuar y reintentar */ }
    });
    cie10Input?.addEventListener("focus", () => cie10Input.setCustomValidity(""));

    const shouldUppercaseControl = (control) => {
        if (!control || control.disabled || control.readOnly) {
            return false;
        }

        if (control.matches("textarea")) {
            return true;
        }

        if (!control.matches("input")) {
            return false;
        }

        const type = String(control.getAttribute("type") || "text").toLowerCase();
        return ["text", "search", "email", "tel", "url"].includes(type);
    };

    const uppercaseControlValue = (control) => {
        if (!shouldUppercaseControl(control)) {
            return;
        }

        const currentValue = control.value || "";
        const uppercaseValue = currentValue.toLocaleUpperCase("es-CO");
        if (currentValue === uppercaseValue) {
            return;
        }

        const selectionStart = control.selectionStart;
        const selectionEnd = control.selectionEnd;
        control.value = uppercaseValue;

        if (selectionStart !== null && selectionEnd !== null) {
            try {
                control.setSelectionRange(selectionStart, selectionEnd);
            } catch {
            }
        }
    };

    document.addEventListener("input", (event) => {
        const control = event.target;
        if (!(control instanceof HTMLElement)
            || !control.closest("#censoNptForm, .censo-filter-panel, .censo-current-patient__search")) {
            return;
        }

        uppercaseControlValue(control);
    });

    const uppercaseAllNptTextFields = () => {
        document
            .querySelectorAll("#censoNptForm input, #censoNptForm textarea, .censo-filter-panel input, .censo-current-patient__search input")
            .forEach((control) => uppercaseControlValue(control));
    };

    const alphanumericIdentificationTypes = new Set(["PA", "CE"]);
    const normalizeDocumentValue = (value) => String(value || "")
        .toUpperCase()
        .replace(/[^A-Z0-9]/g, "")
        .trim();
    const allowsAlphaNumericIdentification = (value) =>
        alphanumericIdentificationTypes.has(String(value || "").trim().toUpperCase());
    const syncNumeroIdentificacionMode = () => {
        if (!numeroIdentificacionInput) return;

        const allowAlphaNumeric = allowsAlphaNumericIdentification(tipoIdentificacionSelect ? tipoIdentificacionSelect.value : "");
        numeroIdentificacionInput.setAttribute("inputmode", allowAlphaNumeric ? "text" : "numeric");
        numeroIdentificacionInput.title = allowAlphaNumeric
            ? "Permite letras y números para PA o CE."
            : "Solo permite dígitos para este tipo de identificación.";

        const currentValue = String(numeroIdentificacionInput.value || "").toUpperCase();
        numeroIdentificacionInput.value = allowAlphaNumeric
            ? currentValue.replace(/[^A-Z0-9]/g, "").slice(0, 20)
            : currentValue.replace(/\D/g, "").slice(0, 20);
    };

    const getAuxiliarValidationMessage = () =>
        document.querySelector("[data-valmsg-for='AuxiliarEnfermeriaAsignado']");

    const getCanonicalAuxiliarValue = (value) => {
        const normalized = String(value || "").trim().toLocaleUpperCase("es-CO");
        if (!normalized) return "";
        return auxiliarEnfermeriaOptions.find((item) =>
            item.trim().toLocaleUpperCase("es-CO") === normalized) || "";
    };

    const syncAuxiliarValidation = (showInvalidMessage = false) => {
        if (!auxiliarEnfermeriaInput) return true;

        const value = String(auxiliarEnfermeriaInput.value || "").trim();
        const canonicalValue = getCanonicalAuxiliarValue(value);
        const messageElement = getAuxiliarValidationMessage();

        if (canonicalValue) {
            auxiliarEnfermeriaInput.value = canonicalValue;
            if (messageElement) messageElement.textContent = "";
            return true;
        }

        if (!value) {
            if (messageElement) messageElement.textContent = "";
            return true;
        }

        if (showInvalidMessage && messageElement) {
            messageElement.textContent = auxiliarEnfermeriaOptions.length === 0
                ? "No hay auxiliares OPS activos para asignar."
                : "Selecciona un auxiliar OPS válido.";
        }

        return false;
    };

    document.querySelectorAll(".npt-phone-input").forEach((input) => {
        input.addEventListener("input", () => {
            input.value = input.value.replace(/\D/g, "").slice(0, 10);
        });
    });

    const syncTelefonoAdicional2Visibility = () => {
        if (!telefonoAdicional2Toggle || !telefonoAdicional2Container) return;
        const shouldShow = telefonoAdicional2Toggle.checked;
        telefonoAdicional2Container.classList.toggle("d-none", !shouldShow);
        if (!shouldShow && telefonoAdicional2Input) {
            telefonoAdicional2Input.value = "";
        }
    };

    if (telefonoAdicional2Toggle && telefonoAdicional2Input?.value) {
        telefonoAdicional2Toggle.checked = true;
    }
    telefonoAdicional2Toggle?.addEventListener("change", syncTelefonoAdicional2Visibility);
    syncTelefonoAdicional2Visibility();

    auxiliarEnfermeriaInput?.addEventListener("input", () => syncAuxiliarValidation(false));
    auxiliarEnfermeriaInput?.addEventListener("change", () => syncAuxiliarValidation(true));
    auxiliarEnfermeriaInput?.addEventListener("blur", () => syncAuxiliarValidation(true));

    numeroIdentificacionInput?.addEventListener("input", syncNumeroIdentificacionMode);
    tipoIdentificacionSelect?.addEventListener("change", syncNumeroIdentificacionMode);
    syncNumeroIdentificacionMode();

    const sanitizeNombrePaciente = () => {
        if (!nombrePacienteInput) return;
        const currentValue = nombrePacienteInput.value || "";
        const sanitizedValue = currentValue.replace(/[^A-Za-zÁÉÍÓÚÜÑáéíóúüñ\s]/g, "");
        if (currentValue !== sanitizedValue) {
            nombrePacienteInput.value = sanitizedValue;
        }
    };

    nombrePacienteInput?.addEventListener("input", sanitizeNombrePaciente);
    sanitizeNombrePaciente();

    const updateDiasTratamiento = () => {
        if (!diasTratamientoInput) return;

        const inicio = fechaInicioNptInput?.value || "";
        const fin = fechaFinNptInput?.value || "";
        if (!inicio || !fin) {
            diasTratamientoInput.value = "";
            return;
        }

        const start = new Date(`${inicio}T00:00:00`);
        const end = new Date(`${fin}T00:00:00`);
        const diffDays = Math.round((end.getTime() - start.getTime()) / 86400000);
        diasTratamientoInput.value = diffDays < 0 ? "" : String(diffDays + 1);
    };

    fechaInicioNptInput?.addEventListener("change", updateDiasTratamiento);
    fechaFinNptInput?.addEventListener("change", updateDiasTratamiento);
    updateDiasTratamiento();

    [cedulaNptFiltroInput, cambiarNptPacienteDocumentoInput].forEach((input) => {
        if (!input) return;
        input.value = normalizeDocumentValue(input.value);
        input.addEventListener("input", () => {
            input.value = normalizeDocumentValue(input.value);
        });
    });
    uppercaseAllNptTextFields();

    const updateEdad = () => {
        if (!fechaNacimientoInput || !edadInput) return;

        const today = new Date();
        const todayValue = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
        if (!fechaNacimientoInput.value || fechaNacimientoInput.value >= todayValue) {
            fechaNacimientoInput.setCustomValidity("La fecha de nacimiento debe ser anterior a la fecha actual.");
        } else {
            fechaNacimientoInput.setCustomValidity("");
        }

        if (!fechaNacimientoInput.value) {
            edadInput.value = "0";
            return;
        }

        const birth = new Date(`${fechaNacimientoInput.value}T00:00:00`);
        let age = today.getFullYear() - birth.getFullYear();
        const monthDiff = today.getMonth() - birth.getMonth();
        if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) {
            age -= 1;
        }
        edadInput.value = Math.max(age, 0).toString();
    };

    fechaNacimientoInput?.addEventListener("change", updateEdad);
    fechaNacimientoInput?.addEventListener("input", updateEdad);
    updateEdad();

    form?.addEventListener("submit", (event) => {
        const submitAction = event.submitter?.getAttribute("formaction") || "";
        if (submitAction.includes("GuardarNptManejo")
            || submitAction.includes("GuardarNptCargueServicios")
            || submitAction.includes("GuardarNptActivoFijo")
            || submitAction.includes("GuardarNptSeguimientoHospitalizado")
            || submitAction.includes("GuardarNptDevolucionProductos")
            || submitAction.includes("GuardarNptAltaPrograma")) {
            return;
        }

        sanitizeNombrePaciente();

        if (!syncAuxiliarValidation(true)) {
            event.preventDefault();
            auxiliarEnfermeriaInput?.focus();
            return;
        }

    });
})();
