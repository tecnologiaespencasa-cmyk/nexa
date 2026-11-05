// Script del programa Cuidadores en la pantalla única del censo. Solo ayuda a diligenciar: lo que de
// verdad se valida lo valida el servidor en cada sección.
(() => {
    // ----- Oportunidad de inicio: días entre el reporte de la EPS y el inicio de la atención -----
    const reporte = document.querySelector("[data-cuidador-fecha='reporte']");
    const inicio = document.querySelector("[data-cuidador-fecha='inicio']");
    const cifra = document.querySelector("[data-cuidador-oportunidad]");
    const unidad = document.querySelector("[data-cuidador-oportunidad-unidad]");

    const diasEntre = (desde, hasta) => {
        if (!desde || !hasta) { return null; }
        // A mediodía UTC para que un cambio de horario no corra el conteo un día.
        const dias = Math.round((Date.parse(`${hasta}T12:00:00Z`) - Date.parse(`${desde}T12:00:00Z`)) / 86400000);
        return Number.isNaN(dias) || dias < 0 ? null : dias;
    };

    const pintarOportunidad = () => {
        if (!cifra) { return; }
        const dias = diasEntre(reporte?.value, inicio?.value);
        cifra.textContent = dias === null ? "—" : String(dias);
        cifra.classList.toggle("censo-recepcion__cifra--vacia", dias === null);
        if (unidad) { unidad.hidden = dias === null; }
    };

    reporte?.addEventListener("change", pintarOportunidad);
    inicio?.addEventListener("change", pintarOportunidad);

    // ----- Campos con formato: se limpia al escribir y al pegar -----
    document.querySelectorAll("[data-cuidador-solo-letras]").forEach((campo) => {
        campo.addEventListener("input", () => {
            const limpio = campo.value.replace(/[^\p{L}\s]/gu, "");
            if (limpio !== campo.value) { campo.value = limpio; }
        });
    });

    document.querySelectorAll("[data-cuidador-solo-numeros]").forEach((campo) => {
        campo.addEventListener("input", () => {
            const limpio = campo.value.replace(/\D/g, "").slice(0, 10);
            if (limpio !== campo.value) { campo.value = limpio; }
        });
    });

    // ----- Misma dirección del paciente -----
    const misma = document.getElementById("cuidadorDireccionMisma");
    const direccion = document.getElementById("cuidadorDireccion");
    if (misma && direccion) {
        let escrita = misma.checked ? "" : direccion.value;
        misma.addEventListener("change", () => {
            if (misma.checked) {
                escrita = direccion.value;
                direccion.value = misma.dataset.direccionPaciente || "";
            } else {
                direccion.value = escrita;
            }
            direccion.readOnly = misma.checked;
        });
    }
})();
