// Script de terapia ambulatoria en la pantalla única del censo.
//
// Vivía en línea en Views/Censo/_TerapiaAmbulatoriaScripts.cshtml y se sacó a este archivo (2026-09-24) para que el navegador lo
// guarde en caché: en línea se descargaba y se volvía a analizar en cada consulta de un paciente.
// Los valores que dependen del servidor (URLs, permisos, datos del registro) los deja esa vista en
// window.NexaCenso.terapia justo antes de cargar este archivo.
(() => {
    const validarDireccionUrl = NexaCenso.terapia.validarDireccionUrl;
    const buscarBarriosUrl = NexaCenso.terapia.buscarBarriosUrl;
    const defaultsMunicipioUrl = NexaCenso.terapia.defaultsMunicipioUrl;
    const form = document.getElementById("terapiaAmbulatoriaForm");
    const csrfTokenInput = form?.querySelector("input[name='__RequestVerificationToken']");
    const direccionInput = document.getElementById("direccionInput");
    const validarDireccionBtn = document.getElementById("validarDireccionBtn");
    const limpiarDireccionBtn = document.getElementById("limpiarDireccionBtn");
    const asumirDireccionCheck = document.getElementById("asumirDireccionErradaCheck");
    const direccionEsValidaInput = document.getElementById("terapiaDireccionEsValidaInput");
    const direccionMessage = document.getElementById("direccionValidacionMensaje");
    const direccionServerMessage = document.getElementById("direccionValidacionServerMensaje");
    const usarSugerenciaServerBtn = document.getElementById("usarSugerenciaDireccionBtnServer");
    const municipioSelect = document.getElementById("municipioResidenciaSelect");
    const clasificacionSelect = document.getElementById("clasificacionZonaSuraSelect");
    const barrioSelect = document.getElementById("barrioSelect");
    const zonaSelect = document.getElementById("zonaDireccionSelect");
    const areaSelect = document.getElementById("areaSelect");
    const codigoCie10Input = document.getElementById("terapiaCodigoCie10Input");
    const diagnosticoInput = document.getElementById("terapiaDiagnosticoInput");
    const tipoIdentificacionSelect = document.getElementById("tipoIdentificacionSelect");
    const numeroIdentificacionInput = document.getElementById("numeroIdentificacionInput");
    const cedulaTerapiaFiltroInput = document.getElementById("cedulaTerapiaFiltroInput");
    const cambiarTerapiaPacienteDocumentoInput = document.getElementById("cambiarTerapiaPacienteDocumento");
    const fechaNacimientoInput = document.getElementById("fechaNacimientoInput");
    const edadInput = document.getElementById("edadInput");
    const cantidadInput = document.getElementById("Cantidad");
    const frecuenciaTerapiaSelect = document.getElementById("FrecuenciaTerapia");
    const segundoTratamientoSwitch = document.getElementById("tieneSegundoTratamientoSwitch");
    const segundoTratamientoContainer = document.getElementById("segundoTratamientoContainer");
    const segundoTratamientoCantidadInput = document.getElementById("SegundoTratamientoCantidad");
    const segundoTratamientoFrecuenciaSelect = document.getElementById("SegundoTratamientoFrecuenciaTerapia");
    const tercerTratamientoSwitch = document.getElementById("tieneTercerTratamientoSwitch");
    const tercerTratamientoSwitchContainer = document.getElementById("tercerTratamientoSwitchContainer");
    const tercerTratamientoContainer = document.getElementById("tercerTratamientoContainer");
    const tercerTratamientoCantidadInput = document.getElementById("TercerTratamientoCantidad");
    const tercerTratamientoFrecuenciaSelect = document.getElementById("TercerTratamientoFrecuenciaTerapia");
    const fechaInicioInput = document.getElementById("fechaInicioInput");
    const fechaFinInput = document.getElementById("fechaFinInput");
    const estadoGestionInput = document.getElementById("estadoGestionInput");
    const gestionSistemaSwitch = document.getElementById("gestionSistemaSwitch");
    const gestionProgress = document.getElementById("gestionProgress");
    const gestionProgressLabel = document.getElementById("gestionProgressLabel");
    const gestionProgressDetail = document.getElementById("gestionProgressDetail");
    const gestionProgressTrack = gestionProgress?.querySelector(".censo-gestion-progress__track");
    const fisioterapeutaInput = document.getElementById("fisioterapeutaInput");
    const telefonoAdicional2Toggle = document.getElementById("telefonoAdicional2Toggle");
    const telefonoAdicional2Container = document.getElementById("telefonoAdicional2Container");
    const telefonoAdicional2Input = document.getElementById("TelefonoAdicional2");
    const fisioterapeutaOptions = Array.from(document.querySelectorAll("#fisioterapeutaOptions option"))
        .map((option) => option.value || "")
        .filter((value) => value.trim());
    let direccionValidada = NexaCenso.terapia.direccionValidada;
    let latestAddressCandidates = [];
    let barriosLoaded = barrioSelect
        ? Array.from(barrioSelect.options).map((option) => (option.value || "").trim()).filter((value) => !!value)
        : [];
    let lastValidationDistrict = "";

    const escapeHtml = (value) => String(value || "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");

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
            || !control.closest("#terapiaAmbulatoriaForm, .censo-filter-panel, .censo-current-patient__search")) {
            return;
        }

        uppercaseControlValue(control);
    });

    const uppercaseAllTerapiaTextFields = () => {
        document
            .querySelectorAll("#terapiaAmbulatoriaForm input, #terapiaAmbulatoriaForm textarea, .censo-filter-panel input, .censo-current-patient__search input")
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

    const getFisioterapeutaValidationMessage = () =>
        document.querySelector("[data-valmsg-for='Fisioterapeuta']");

    const getCanonicalFisioterapeutaValue = (value) => {
        const normalized = String(value || "").trim().toLocaleUpperCase("es-CO");
        if (!normalized) return "";
        return fisioterapeutaOptions.find((item) =>
            item.trim().toLocaleUpperCase("es-CO") === normalized) || "";
    };

    const syncFisioterapeutaValidation = (showInvalidMessage = false) => {
        if (!fisioterapeutaInput) return true;

        const value = String(fisioterapeutaInput.value || "").trim();
        const canonicalValue = getCanonicalFisioterapeutaValue(value);
        const messageElement = getFisioterapeutaValidationMessage();

        if (canonicalValue) {
            fisioterapeutaInput.value = canonicalValue;
            if (messageElement) messageElement.textContent = "";
            return true;
        }

        if (!value) {
            if (messageElement) messageElement.textContent = "";
            return true;
        }

        if (showInvalidMessage && messageElement) {
            messageElement.textContent = fisioterapeutaOptions.length === 0
                ? "No hay auxiliares OPS activos para asignar."
                : "Selecciona un auxiliar OPS válido.";
        }

        return false;
    };

    const setDireccionValidada = (value) => {
        direccionValidada = value;
        if (direccionEsValidaInput) direccionEsValidaInput.value = value ? "true" : "false";
    };

    const clearServerDireccionMessage = () => {
        if (direccionServerMessage) direccionServerMessage.remove();
    };

    const setDireccionMessage = (type, html) => {
        if (!direccionMessage) return;
        direccionMessage.className = `alert alert-${type} mt-2`;
        direccionMessage.innerHTML = html;
        direccionMessage.classList.remove("d-none");
    };

    const clearDireccionMessage = () => {
        if (!direccionMessage) return;
        direccionMessage.className = "alert d-none mt-2";
        direccionMessage.innerHTML = "";
    };

    const syncSelectPlaceholderState = (select) => {
        if (!select) return;
        select.classList.toggle("is-placeholder", !select.value);
    };

    const setSelectValue = (select, value) => {
        if (!select) return;
        const normalizedValue = String(value || "").trim();
        if (!normalizedValue) {
            select.value = "";
            syncSelectPlaceholderState(select);
            return;
        }

        const existing = Array.from(select.options).some((option) => option.value === normalizedValue);
        if (!existing) {
            select.appendChild(new Option(normalizedValue, normalizedValue));
        }
        select.value = normalizedValue;
        syncSelectPlaceholderState(select);
    };

    const setBarrioOptions = (options, selectedValue, preserveCurrentSelection = true) => {
        if (!barrioSelect) return;

        const currentValue = preserveCurrentSelection ? (barrioSelect.value || "") : "";
        const targetValue = String(selectedValue ?? currentValue ?? "").trim();
        const uniqueOptions = Array.from(new Set((options || [])
            .map((x) => (x || "").trim())
            .filter((x) => x)));

        barrioSelect.innerHTML = "";
        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = "Selecciona barrio...";
        placeholder.disabled = true;
        placeholder.hidden = true;
        barrioSelect.appendChild(placeholder);

        uniqueOptions.forEach((opt) => {
            const option = document.createElement("option");
            option.value = opt;
            option.textContent = opt;
            barrioSelect.appendChild(option);
        });

        if (targetValue) {
            const hasTarget = uniqueOptions.some((opt) => opt.toLowerCase() === targetValue.toLowerCase());
            if (!hasTarget) {
                barrioSelect.appendChild(new Option(targetValue, targetValue));
            }
            barrioSelect.value = targetValue;
        }

        syncSelectPlaceholderState(barrioSelect);
    };

    const applyMunicipalityDefaults = async (municipio, barrio, district) => {
        if (!municipio) return;

        try {
            const url = new URL(defaultsMunicipioUrl, window.location.origin);
            url.searchParams.set("municipio", municipio);
            if (barrio) url.searchParams.set("barrio", barrio);
            if (district) url.searchParams.set("district", district);
            if (direccionInput?.value) url.searchParams.set("direccion", direccionInput.value);

            const response = await fetch(url);
            if (!response.ok) return;

            const data = await response.json();
            setSelectValue(clasificacionSelect, data.clasificacionZonaSura);
            setSelectValue(zonaSelect, data.zonaDireccionSegunMunicipio);
        } catch {
        }
    };

    const loadBarriosByMunicipio = async (municipio, term, selectedBarrio, preserveCurrentSelection = true) => {
        if (!municipio) {
            barriosLoaded = [];
            setBarrioOptions([], "", false);
            return;
        }

        try {
            const response = await fetch(`${buscarBarriosUrl}?municipio=${encodeURIComponent(municipio)}&term=${encodeURIComponent(term || "a")}`);
            if (!response.ok) {
                barriosLoaded = [];
                setBarrioOptions([], "", false);
                return;
            }

            const data = await response.json();
            barriosLoaded = (data.neighborhoods || []).filter((x) => !!x);
            setBarrioOptions(barriosLoaded, selectedBarrio, preserveCurrentSelection);
        } catch {
            barriosLoaded = [];
            setBarrioOptions([], "", false);
        }
    };

    const clearAddressDerivedFields = () => {
        lastValidationDistrict = "";
        barriosLoaded = [];
        setBarrioOptions([], "", false);
        setSelectValue(clasificacionSelect, "");
        setSelectValue(municipioSelect, "");
        setSelectValue(zonaSelect, "");
        setSelectValue(areaSelect, "");
        if (asumirDireccionCheck) asumirDireccionCheck.checked = false;
    };

    const clearAllAddressData = () => {
        if (direccionInput) direccionInput.value = "";
        setDireccionValidada(false);
        latestAddressCandidates = [];
        clearAddressDerivedFields();
        clearDireccionMessage();
        clearServerDireccionMessage();
    };

    const applyAddressCandidate = async (candidate) => {
        if (!candidate) return;

        const selectedMunicipality = candidate.municipality || "";
        const selectedNeighborhood = (candidate.neighborhood || candidate.district || "").trim();
        lastValidationDistrict = candidate.district || "";

        if (direccionInput && candidate.formattedAddress && !asumirDireccionCheck?.checked) {
            direccionInput.value = candidate.formattedAddress;
        }

        if (municipioSelect) {
            const hasOption = selectedMunicipality
                ? Array.from(municipioSelect.options).some((x) => x.value === selectedMunicipality)
                : false;
            municipioSelect.value = hasOption ? selectedMunicipality : "";
        }

        await applyMunicipalityDefaults(selectedMunicipality, selectedNeighborhood, lastValidationDistrict);
        await loadBarriosByMunicipio(selectedMunicipality, selectedNeighborhood || "a", selectedNeighborhood, false);

        if (barrioSelect) {
            setBarrioOptions(barriosLoaded, selectedNeighborhood, false);
        }

        setSelectValue(zonaSelect, candidate.zonaDireccionSegunMunicipio);
        setSelectValue(clasificacionSelect, candidate.clasificacionZonaSura);

        setDireccionValidada(true);
        latestAddressCandidates = [];
        setDireccionMessage("success", "Direccion seleccionada correctamente desde las coincidencias de Google.");
    };

    const renderAddressCandidates = (candidates, message) => {
        const safeCandidates = (candidates || []).filter((x) => !!x && !!x.formattedAddress);
        latestAddressCandidates = safeCandidates;

        if (safeCandidates.length === 0) return false;

        const optionsHtml = safeCandidates
            .map((candidate, index) => {
                const municipio = candidate.municipality ? ` (${escapeHtml(candidate.municipality)})` : "";
                return `<button type="button" class="btn btn-sm btn-outline-secondary d-block text-start w-100 mb-1 js-direccion-candidate" data-index="${index}">${escapeHtml(candidate.formattedAddress)}${municipio}</button>`;
            })
            .join("");

        setDireccionMessage(
            "warning",
            `${escapeHtml(message || "Selecciona la direccion correcta antes de continuar:")}<div class="mt-2">${optionsHtml}</div>`);

        document.querySelectorAll(".js-direccion-candidate").forEach((button) => {
            button.addEventListener("click", async () => {
                const index = Number(button.getAttribute("data-index"));
                if (!Number.isFinite(index) || index < 0 || index >= latestAddressCandidates.length) return;
                await applyAddressCandidate(latestAddressCandidates[index]);
            });
        });

        return true;
    };

    if (validarDireccionBtn && direccionInput && csrfTokenInput) {
        validarDireccionBtn.addEventListener("click", async () => {
            if (!direccionInput.value.trim()) {
                setDireccionMessage("warning", "Debes escribir una dirección para validarla.");
                return;
            }

            validarDireccionBtn.disabled = true;
            clearDireccionMessage();
            clearServerDireccionMessage();
            try {
                const response = await fetch(validarDireccionUrl, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": csrfTokenInput.value
                    },
                    body: JSON.stringify({ direccion: direccionInput.value.trim() })
                });
                if (!response.ok) {
                    throw new Error("No se pudo validar la dirección.");
                }

                const data = await response.json();
                setDireccionValidada(data.isValid === true);
                latestAddressCandidates = [];

                const shouldClearDerivedData = data.outcome === "invalid" || data.outcome === "unavailable";
                const hasAddressCandidates = data.requiresSelection === true
                    && Array.isArray(data.candidates)
                    && data.candidates.length > 0;
                const suggestedBarrio = (data.neighborhood || data.district || "").trim();

                if (shouldClearDerivedData) {
                    clearAddressDerivedFields();
                }

                if (!shouldClearDerivedData && hasAddressCandidates) {
                    clearAddressDerivedFields();
                    setDireccionValidada(false);
                    renderAddressCandidates(data.candidates, data.message);
                    return;
                }

                if (!shouldClearDerivedData && municipioSelect && data.municipality) {
                    const hasOption = Array.from(municipioSelect.options).some((x) => x.value === data.municipality);
                    if (hasOption) {
                        if (barrioSelect) barrioSelect.value = "";
                        municipioSelect.value = data.municipality;
                        syncSelectPlaceholderState(municipioSelect);
                        lastValidationDistrict = data.district || "";
                        await applyMunicipalityDefaults(data.municipality, suggestedBarrio, lastValidationDistrict);
                        await loadBarriosByMunicipio(data.municipality, suggestedBarrio || "a", suggestedBarrio, false);
                    }
                }

                if (!shouldClearDerivedData && barrioSelect) {
                    setBarrioOptions(barriosLoaded, suggestedBarrio, false);
                }

                if (!shouldClearDerivedData && zonaSelect && data.zonaDireccionSegunMunicipio) {
                    zonaSelect.value = data.zonaDireccionSegunMunicipio;
                    syncSelectPlaceholderState(zonaSelect);
                }

                if (!shouldClearDerivedData && clasificacionSelect && data.clasificacionZonaSura) {
                    clasificacionSelect.value = data.clasificacionZonaSura;
                    syncSelectPlaceholderState(clasificacionSelect);
                }

                if (data.isValid) {
                    if (data.formattedAddress && !asumirDireccionCheck?.checked) {
                        direccionInput.value = data.formattedAddress;
                    }
                    setDireccionMessage("success", data.message || "Direccion validada correctamente.");
                    return;
                }

                let html = data.message || "La dirección no pudo ser validada.";
                if (data.suggestedAddress) {
                    html += `<br/>Sugerencia: <strong>${escapeHtml(data.suggestedAddress)}</strong> <button type="button" class="btn btn-sm btn-link p-0 ms-2" id="usarSugerenciaDireccionBtnClient">Usar sugerencia</button>`;
                }
                setDireccionMessage("warning", html);
                document.getElementById("usarSugerenciaDireccionBtnClient")?.addEventListener("click", (event) => {
                    event.preventDefault();
                    direccionInput.value = data.suggestedAddress || "";
                    setDireccionValidada(false);
                });
            } catch {
                clearAddressDerivedFields();
                setDireccionMessage("danger", "No fue posible validar la dirección. Intenta nuevamente o marca 'Asumir dirección errada y continuar'.");
                setDireccionValidada(false);
            } finally {
                validarDireccionBtn.disabled = false;
            }
        });
    }

    if (direccionInput) {
        direccionInput.addEventListener("input", () => {
            setDireccionValidada(false);
            clearServerDireccionMessage();
        });
    }

    if (limpiarDireccionBtn && direccionInput) {
        limpiarDireccionBtn.addEventListener("click", () => {
            clearAllAddressData();
        });
    }

    usarSugerenciaServerBtn?.addEventListener("click", () => {
        const suggested = usarSugerenciaServerBtn.previousElementSibling?.textContent || "";
        if (direccionInput && suggested) {
            direccionInput.value = suggested.trim();
            setDireccionValidada(false);
        }
    });

    municipioSelect?.addEventListener("change", async () => {
        const municipio = municipioSelect.value || "";
        lastValidationDistrict = "";
        if (barrioSelect) barrioSelect.value = "";
        await applyMunicipalityDefaults(municipio, "", "");
        await loadBarriosByMunicipio(municipio, "a", "");
    });

    barrioSelect?.addEventListener("change", async () => {
        const municipio = municipioSelect ? municipioSelect.value : "";
        const barrio = barrioSelect.value || "";
        await applyMunicipalityDefaults(municipio, barrio, lastValidationDistrict);
    });

    document.querySelectorAll(".terapia-phone-input").forEach((input) => {
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

    fisioterapeutaInput?.addEventListener("input", () => syncFisioterapeutaValidation(false));
    fisioterapeutaInput?.addEventListener("change", () => syncFisioterapeutaValidation(true));
    fisioterapeutaInput?.addEventListener("blur", () => syncFisioterapeutaValidation(true));

    numeroIdentificacionInput?.addEventListener("input", syncNumeroIdentificacionMode);
    tipoIdentificacionSelect?.addEventListener("change", syncNumeroIdentificacionMode);
    syncNumeroIdentificacionMode();

    [cedulaTerapiaFiltroInput, cambiarTerapiaPacienteDocumentoInput].forEach((input) => {
        if (!input) return;
        input.value = normalizeDocumentValue(input.value);
        input.addEventListener("input", () => {
            input.value = normalizeDocumentValue(input.value);
        });
    });
    uppercaseAllTerapiaTextFields();

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

    const getTerapiasPorSemana = (frecuencia) => {
        const normalized = String(frecuencia || "").trim().toLocaleLowerCase("es-CO");
        if (normalized === "diaria") return 5;
        if (normalized === "tres veces por semana") return 3;
        if (normalized === "dos veces por semana") return 2;
        if (normalized === "una vez por semana") return 1;
        return 0;
    };

    const formatDateInputValue = (date) => {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${year}-${month}-${day}`;
    };

    const calculateTreatmentFechaFin = (cantidadElement, frecuenciaElement) => {
        if (!fechaInicioInput?.value || !cantidadElement || !frecuenciaElement) return null;

        const cantidad = Number.parseInt(cantidadElement.value || "", 10);
        const terapiasPorSemana = getTerapiasPorSemana(frecuenciaElement.value);
        if (!Number.isFinite(cantidad) || cantidad < 1 || terapiasPorSemana < 1) return null;

        const semanas = Math.ceil(cantidad / terapiasPorSemana);
        const fechaFin = new Date(`${fechaInicioInput.value}T00:00:00`);
        fechaFin.setDate(fechaFin.getDate() + (semanas * 7));
        return fechaFin;
    };

    const updateFechaFin = () => {
        if (!fechaFinInput) return;

        const dates = [
            calculateTreatmentFechaFin(cantidadInput, frecuenciaTerapiaSelect)
        ];

        if (segundoTratamientoSwitch?.checked) {
            dates.push(calculateTreatmentFechaFin(segundoTratamientoCantidadInput, segundoTratamientoFrecuenciaSelect));
        }

        if (segundoTratamientoSwitch?.checked && tercerTratamientoSwitch?.checked) {
            dates.push(calculateTreatmentFechaFin(tercerTratamientoCantidadInput, tercerTratamientoFrecuenciaSelect));
        }

        const validDates = dates.filter((date) => date instanceof Date && !Number.isNaN(date.getTime()));
        if (validDates.length === 0) {
            fechaFinInput.value = "";
            return;
        }

        validDates.sort((a, b) => b.getTime() - a.getTime());
        fechaFinInput.value = formatDateInputValue(validDates[0]);
    };

    cantidadInput?.addEventListener("input", updateFechaFin);
    frecuenciaTerapiaSelect?.addEventListener("change", updateFechaFin);
    segundoTratamientoCantidadInput?.addEventListener("input", updateFechaFin);
    segundoTratamientoFrecuenciaSelect?.addEventListener("change", updateFechaFin);
    tercerTratamientoCantidadInput?.addEventListener("input", updateFechaFin);
    tercerTratamientoFrecuenciaSelect?.addEventListener("change", updateFechaFin);
    fechaInicioInput?.addEventListener("change", updateFechaFin);

    const clearOptionalTreatmentFields = (container) => {
        container?.querySelectorAll("input[type='checkbox']").forEach((input) => {
            input.checked = false;
        });
        container?.querySelectorAll("input[type='number'], select").forEach((input) => {
            input.value = "";
            syncSelectPlaceholderState(input);
        });
    };

    const syncTreatmentVisibility = () => {
        const hasSecond = segundoTratamientoSwitch?.checked === true;
        const hasThird = hasSecond && tercerTratamientoSwitch?.checked === true;

        segundoTratamientoContainer?.classList.toggle("d-none", !hasSecond);
        tercerTratamientoSwitchContainer?.classList.toggle("d-none", !hasSecond);
        if (!hasSecond) {
            clearOptionalTreatmentFields(segundoTratamientoContainer);
            if (tercerTratamientoSwitch) tercerTratamientoSwitch.checked = false;
        }

        if (tercerTratamientoSwitch) {
            tercerTratamientoSwitch.disabled = !hasSecond;
        }

        tercerTratamientoContainer?.classList.toggle("d-none", !hasThird);
        if (!hasThird) {
            clearOptionalTreatmentFields(tercerTratamientoContainer);
        }

        updateFechaFin();
    };

    segundoTratamientoSwitch?.addEventListener("change", syncTreatmentVisibility);
    tercerTratamientoSwitch?.addEventListener("change", syncTreatmentVisibility);
    syncTreatmentVisibility();
    updateFechaFin();

    const gestionStates = {
        pending: {
            value: "Pendiente confirmar datos",
            className: "is-pending",
            step: 1,
            detail: "Pendiente confirmar datos"
        },
        confirmed: {
            value: "Datos confirmados",
            className: "is-confirmed",
            step: 2,
            detail: "Datos confirmados"
        },
        complete: {
            value: "Gestión completa",
            className: "is-complete",
            step: 3,
            detail: "Gestión completa"
        }
    };

    // Cada campo se busca por su name DENTRO de este formulario. En la pantalla única conviven
    // los cinco programas: varios ids se repiten en otros formularios y dos ya no existían
    // (tipoIdentificacionSelect y numeroIdentificacionInput se fueron al maestro), así que con
    // getElementById la barra se quedaba siempre en "Pendiente". La lista es la misma de
    // HasTerapiaAmbulatoriaDatosConfirmados en el servidor, para que ambos digan lo mismo.
    const campoTerapia = (nombre) => form?.querySelector(`[name='${nombre}']`) || null;
    const marcadosTerapia = (nombre) => form
        ? form.querySelectorAll(`input[name='${nombre}']:checked`).length
        : 0;
    const hasFieldValue = (element) => !!String(element?.value || "").trim();
    const hasPositiveNumber = (element) => Number(String(element?.value || "").trim()) > 0;
    const hasOptionalTreatmentData = (switchElement, cantidadNombre, frecuenciaNombre, tiposNombre) => {
        if (switchElement?.checked !== true) return true;
        return hasPositiveNumber(campoTerapia(cantidadNombre))
            && hasFieldValue(campoTerapia(frecuenciaNombre))
            && marcadosTerapia(tiposNombre) > 0;
    };

    const camposRequeridosGestion = [
        "NombrePaciente",
        "TipoIdentificacion",
        "NumeroIdentificacion",
        "CorreoElectronico",
        "FrecuenciaTerapia",
        "CodigoCie10",
        "DiagnosticoDescriptivo",
        "NumeroAutorizacion",
        "IpsQueRemite",
        "TelefonoPrincipal",
        "TelefonoAdicional1",
        "EstadoPaciente",
        "FechaFin"
    ];

    const hasConfirmedBaseData = () => {
        const hasRequiredValues = camposRequeridosGestion.every((nombre) => hasFieldValue(campoTerapia(nombre)))
            && hasPositiveNumber(campoTerapia("Cantidad"));
        const hasTipoTerapia = marcadosTerapia("TiposTerapiaSeleccionados") > 0;
        const hasSecondTreatment = hasOptionalTreatmentData(
            segundoTratamientoSwitch,
            "SegundoTratamientoCantidad",
            "SegundoTratamientoFrecuenciaTerapia",
            "SegundoTratamientoTiposTerapiaSeleccionados");
        const hasThirdTreatment = segundoTratamientoSwitch?.checked === true
            ? hasOptionalTreatmentData(
                tercerTratamientoSwitch,
                "TercerTratamientoCantidad",
                "TercerTratamientoFrecuenciaTerapia",
                "TercerTratamientoTiposTerapiaSeleccionados")
            : true;
        return hasRequiredValues && hasTipoTerapia && hasSecondTreatment && hasThirdTreatment;
    };

    const resolveGestionState = () => {
        if (!hasConfirmedBaseData()) return gestionStates.pending;

        const hasFisioterapeuta = !!getCanonicalFisioterapeutaValue(fisioterapeutaInput?.value || "");
        if (!hasFisioterapeuta || !gestionSistemaSwitch?.checked) return gestionStates.confirmed;

        return gestionStates.complete;
    };

    const updateGestionProgress = () => {
        updateFechaFin();
        const state = resolveGestionState();
        if (estadoGestionInput) estadoGestionInput.value = state.value;
        if (gestionProgressLabel) gestionProgressLabel.textContent = state.value;
        if (gestionProgressDetail) gestionProgressDetail.textContent = state.detail;
        if (gestionProgressTrack) gestionProgressTrack.setAttribute("aria-valuenow", state.step.toString());

        if (gestionProgress) {
            gestionProgress.classList.remove("is-pending", "is-confirmed", "is-complete");
            gestionProgress.classList.add(state.className);
        }

        gestionProgress?.querySelectorAll(".censo-gestion-progress__step").forEach((stepElement) => {
            const step = Number(stepElement.getAttribute("data-step"));
            stepElement.classList.toggle("is-done", step < state.step);
            stepElement.classList.toggle("is-active", step === state.step);
        });
    };

    form?.addEventListener("input", updateGestionProgress);
    form?.addEventListener("change", updateGestionProgress);
    updateGestionProgress();

    form?.addEventListener("submit", (event) => {
        const submitAction = event.submitter?.getAttribute("formaction") || "";
        if (submitAction.includes("GuardarTerapiaAmbulatoriaProrroga")
            || submitAction.includes("GuardarTerapiaAmbulatoriaGestionAlta")
            || submitAction.includes("SubirTerapiaAmbulatoriaAdjuntos")) {
            return;
        }

        updateGestionProgress();

        if (!syncFisioterapeutaValidation(true)) {
            event.preventDefault();
            fisioterapeutaInput?.focus();
            return;
        }

        if (direccionInput?.value.trim() && !direccionValidada && !asumirDireccionCheck?.checked) {
            event.preventDefault();
            setDireccionMessage("warning", "Debes validar la dirección o marcar 'Asumir dirección errada y continuar'.");
            direccionInput?.focus();
        }
    });

    // ----- Alta del paciente -----
    // El alta es solo el Estado del paciente. Al elegir "Alta" se abre una ventana que pide
    // fecha y motivo, los copia en Gestión alta y los guarda en el acto. Si se cierra sin
    // guardar, el estado vuelve al que tenía: nunca queda "Alta" en pantalla sin estar
    // guardado.
    const guardarAltaUrl = NexaCenso.terapia.guardarAltaUrl;
    const estadoAltaValor = "Alta";
    const estadoPacienteSelect = campoTerapia("EstadoPaciente");
    const altaDialogoEl = document.getElementById("terapiaAltaDialogo");
    const altaDialogo = altaDialogoEl && window.bootstrap
        ? window.bootstrap.Modal.getOrCreateInstance(altaDialogoEl)
        : null;
    const altaFechaInput = document.getElementById("terapiaAltaFecha");
    const altaGuardarBtn = document.getElementById("terapiaAltaGuardar");
    const altaErrorEl = document.getElementById("terapiaAltaError");
    const panelDatosEspecificos = document.getElementById("tab-terapia-datos-especificos");
    const camposFueraDeHuella = new Set(["EstadoPaciente", "EstadoGestion", "FechaAlta", "MotivoAlta"]);

    let estadoPacienteAnterior = estadoPacienteSelect?.value || "";
    let altaConfirmada = false;

    const idRegistroTerapia = () => String(campoTerapia("EditingRecordId")?.value || "").trim();
    const motivoDialogo = () =>
        altaDialogoEl?.querySelector("input[name='terapiaAltaMotivo']:checked")?.value || "";

    // Lo que guarda "Actualizar registro": si el usuario tocó algo aquí antes de elegir Alta,
    // el alta se guarda junto con eso en vez de recargar la página y perderlo.
    const huellaDatosEspecificos = () => panelDatosEspecificos
        ? Array.from(panelDatosEspecificos.querySelectorAll("input[name], select[name], textarea[name]"))
            .filter((control) => control.type !== "file" && !camposFueraDeHuella.has(control.name))
            .map((control) => {
                const valor = control.type === "checkbox" || control.type === "radio"
                    ? (control.checked ? control.value : "")
                    : control.value;
                return `${control.name}=${valor}`;
            })
            .join("&")
        : "";

    const mostrarErrorAlta = (mensaje) => {
        if (!altaErrorEl) return;
        altaErrorEl.textContent = mensaje || "";
        altaErrorEl.classList.toggle("d-none", !mensaje);
    };

    // Fecha y motivo del alta dentro de este formulario, en Gestión alta. En un registro guardado
    // son sus campos editables; en un ingreso nuevo viajan dos ocultos y la sección muestra
    // copias de consulta (deshabilitadas, no se envían).
    const camposAltaFormulario = () => ({
        fecha: form?.querySelector("#terapiaFechaAltaInput") || null,
        motivos: form ? Array.from(form.querySelectorAll("input[type='radio'][name='MotivoAlta']")) : [],
        motivoOculto: form?.querySelector("#terapiaMotivoAltaOculto") || null,
        fechaVista: form?.querySelector("#terapiaFechaAltaVista") || null,
        motivosVista: form ? Array.from(form.querySelectorAll("input[type='radio'][name='MotivoAltaVista']")) : []
    });

    const altaEnFormulario = () => {
        const { fecha, motivos, motivoOculto } = camposAltaFormulario();
        return {
            fecha: fecha?.value || "",
            motivo: motivos.find((radio) => radio.checked)?.value || motivoOculto?.value || ""
        };
    };

    const escribirAltaEnFormulario = (fechaAlta, motivoAlta) => {
        const { fecha, motivos, motivoOculto, fechaVista, motivosVista } = camposAltaFormulario();
        if (fecha) fecha.value = fechaAlta || "";
        if (fechaVista) fechaVista.value = fechaAlta || "";
        motivos.forEach((radio) => { radio.checked = radio.value === motivoAlta; });
        motivosVista.forEach((radio) => { radio.checked = radio.value === motivoAlta; });
        if (motivoOculto) motivoOculto.value = motivoAlta || "";
    };

    const copiarAltaAlFormulario = () => escribirAltaEnFormulario(altaFechaInput?.value || "", motivoDialogo());

    // Lo que tenía Gestión alta antes de abrir la ventana: cancelar lo devuelve tal cual.
    let altaAntesDelDialogo = { fecha: "", motivo: "" };

    const validarDialogoAlta = () => {
        const fecha = altaFechaInput?.value || "";
        if (!fecha) return "Selecciona la fecha de alta.";
        if (altaFechaInput?.max && fecha > altaFechaInput.max) return "La fecha de alta no puede ser futura.";
        const ingreso = String(campoTerapia("FechaIngreso")?.value || "");
        if (ingreso && fecha < ingreso) return "La fecha de alta no puede ser anterior a la fecha de ingreso.";
        if (!motivoDialogo()) return "Selecciona el motivo del alta.";
        return "";
    };

    const abrirDialogoAlta = () => {
        altaAntesDelDialogo = altaEnFormulario();
        if (altaFechaInput) {
            altaFechaInput.value = altaAntesDelDialogo.fecha || altaFechaInput.max || "";
        }
        altaDialogoEl?.querySelectorAll("input[name='terapiaAltaMotivo']").forEach((radio) => {
            radio.checked = radio.value === altaAntesDelDialogo.motivo;
        });
        mostrarErrorAlta("");
        altaConfirmada = false;
        altaDialogo?.show();
    };

    estadoPacienteSelect?.addEventListener("change", () => {
        const valor = estadoPacienteSelect.value;
        if (valor === estadoAltaValor && estadoPacienteAnterior !== estadoAltaValor && altaDialogo) {
            abrirDialogoAlta();
            return;
        }
        // Paciente activo = Gestión alta vacía (el servidor también la vacía al guardar).
        if (valor !== estadoAltaValor && estadoPacienteAnterior === estadoAltaValor) {
            escribirAltaEnFormulario("", "");
        }
        estadoPacienteAnterior = valor;
    });

    altaDialogoEl?.addEventListener("shown.bs.modal", () => altaFechaInput?.focus());

    altaDialogoEl?.addEventListener("hidden.bs.modal", () => {
        if (altaConfirmada || !estadoPacienteSelect) return;
        estadoPacienteSelect.value = estadoPacienteAnterior;
        estadoPacienteSelect.dispatchEvent(new Event("change", { bubbles: true }));
        escribirAltaEnFormulario(altaAntesDelDialogo.fecha, altaAntesDelDialogo.motivo);
    });

    altaDialogoEl?.addEventListener("change", () => {
        mostrarErrorAlta("");
        copiarAltaAlFormulario();
    });

    altaGuardarBtn?.addEventListener("click", async () => {
        const error = validarDialogoAlta();
        if (error) {
            mostrarErrorAlta(error);
            return;
        }

        copiarAltaAlFormulario();
        const registroId = idRegistroTerapia();

        // Registro nuevo, o con otros cambios sin guardar en Datos específicos: se guarda el
        // formulario entero, que ya lleva el alta. El servidor exige fecha y motivo igual.
        if (!registroId || huellaDatosEspecificos() !== huellaInicialDatosEspecificos) {
            altaConfirmada = true;
            estadoPacienteAnterior = estadoAltaValor;
            altaDialogo?.hide();
            form?.requestSubmit();
            return;
        }

        altaGuardarBtn.disabled = true;
        try {
            const datos = new FormData();
            datos.append("id", registroId);
            datos.append("fechaAlta", altaFechaInput?.value || "");
            datos.append("motivoAlta", motivoDialogo());
            if (csrfTokenInput?.value) {
                datos.append("__RequestVerificationToken", csrfTokenInput.value);
            }

            const respuesta = await fetch(guardarAltaUrl, { method: "POST", body: datos });
            const cuerpo = await respuesta.json().catch(() => ({}));
            if (!respuesta.ok) {
                mostrarErrorAlta(cuerpo.message || "No se pudo guardar el alta. Intenta de nuevo.");
                return;
            }

            altaConfirmada = true;
            estadoPacienteAnterior = estadoAltaValor;
            window.location.assign(cuerpo.redirectUrl || window.location.href);
        } catch {
            mostrarErrorAlta("No hay conexión con el servidor. El alta no se guardó.");
        } finally {
            altaGuardarBtn.disabled = false;
        }
    });

    // Se toma al final, cuando el resto del script ya normalizó los campos al cargar.
    const huellaInicialDatosEspecificos = huellaDatosEspecificos();
})();
