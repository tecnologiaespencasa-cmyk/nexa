// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.querySelectorAll("[data-password-toggle]").forEach((toggleButton) => {
  const inputId = toggleButton.getAttribute("data-password-input");
  const passwordInput = inputId ? document.getElementById(inputId) : null;

  if (!passwordInput) {
    return;
  }

  toggleButton.addEventListener("click", () => {
    const isVisible = passwordInput.getAttribute("type") === "text";

    passwordInput.setAttribute("type", isVisible ? "password" : "text");
    toggleButton.classList.toggle("is-visible", !isVisible);
    toggleButton.setAttribute("aria-label", isVisible ? "Mostrar contraseña" : "Ocultar contraseña");
    passwordInput.focus();
  });
});

// ════════════════════════════════════════════════════════════════════════════
// Navbar en una sola línea.
// Los módulos que no caben en el ancho disponible se ocultan de la barra y se
// reconstruyen dentro del menú "Más", de forma que nunca se produzca un salto
// de línea sin importar cuántos módulos tenga el usuario.
// ════════════════════════════════════════════════════════════════════════════
(() => {
  "use strict";

  const navList = document.querySelector(".intranet-nav-links");
  const overflowItem = navList ? navList.querySelector("[data-nav-overflow]") : null;
  const overflowMenu = overflowItem ? overflowItem.querySelector("[data-nav-overflow-menu]") : null;

  if (!navList || !overflowItem || !overflowMenu) {
    return;
  }

  const expandedQuery = window.matchMedia("(min-width: 1200px)");
  const items = Array.from(navList.children).filter((item) => item !== overflowItem);

  // Describe cada módulo una sola vez para poder reconstruirlo en el menú "Más".
  const descriptors = items.map((item) => {
    const link = item.querySelector("a.intranet-nav-link");
    if (link) {
      return {
        item,
        kind: "link",
        label: link.querySelector("span:last-child")?.textContent?.trim() ?? "",
        href: link.getAttribute("href") ?? "#",
        active: link.classList.contains("active")
      };
    }

    const toggle = item.querySelector("button.intranet-nav-link");
    return {
      item,
      kind: "group",
      label: toggle?.querySelector("span:last-child")?.textContent?.trim() ?? "",
      children: Array.from(item.querySelectorAll(".dropdown-menu a")).map((child) => ({
        label: child.querySelector("span")?.textContent?.trim() ?? child.textContent.trim(),
        href: child.getAttribute("href") ?? "#",
        active: child.classList.contains("active")
      }))
    };
  });

  const gap = () => parseFloat(window.getComputedStyle(navList).columnGap || "0") || 0;

  function buildOverflowMenu(hidden) {
    overflowMenu.replaceChildren();

    hidden.forEach((descriptor) => {
      if (descriptor.kind === "group") {
        const header = document.createElement("li");
        const headerText = document.createElement("span");
        headerText.className = "dropdown-header";
        headerText.textContent = descriptor.label;
        header.appendChild(headerText);
        overflowMenu.appendChild(header);

        descriptor.children.forEach((child) => {
          overflowMenu.appendChild(
            buildMenuEntry(child.label, child.href, child.active, "intranet-nav-overflow__child")
          );
        });
        return;
      }

      overflowMenu.appendChild(buildMenuEntry(descriptor.label, descriptor.href, descriptor.active, ""));
    });
  }

  function buildMenuEntry(label, href, active, extraClass) {
    const listItem = document.createElement("li");
    const anchor = document.createElement("a");
    anchor.className = `dropdown-item intranet-censo-dropdown__item${extraClass ? ` ${extraClass}` : ""}`;
    if (active) {
      anchor.classList.add("active");
    }
    anchor.href = href;
    anchor.textContent = label;
    listItem.appendChild(anchor);
    return listItem;
  }

  function layout() {
    // Estado base: todo visible y sin botón "Más" para poder medir de verdad.
    descriptors.forEach((descriptor) => descriptor.item.removeAttribute("hidden"));
    overflowItem.setAttribute("hidden", "");

    if (!expandedQuery.matches) {
      return;
    }

    const spacing = gap();
    const available = navList.clientWidth;
    const widths = descriptors.map((descriptor) => descriptor.item.offsetWidth);
    const totalWidth = widths.reduce((sum, width) => sum + width, 0) + spacing * Math.max(descriptors.length - 1, 0);

    if (totalWidth <= available) {
      return;
    }

    overflowItem.removeAttribute("hidden");
    const budget = available - overflowItem.offsetWidth - spacing;

    let used = 0;
    let firstHidden = 0;

    for (let index = 0; index < descriptors.length; index += 1) {
      const width = widths[index] + (index > 0 ? spacing : 0);
      if (used + width > budget) {
        break;
      }
      used += width;
      firstHidden = index + 1;
    }

    const hidden = descriptors.slice(firstHidden);
    hidden.forEach((descriptor) => descriptor.item.setAttribute("hidden", ""));
    buildOverflowMenu(hidden);
  }

  let pending = 0;
  const scheduleLayout = () => {
    window.cancelAnimationFrame(pending);
    pending = window.requestAnimationFrame(layout);
  };

  scheduleLayout();
  window.addEventListener("resize", scheduleLayout);
  expandedQuery.addEventListener("change", scheduleLayout);

  // Las fuentes web cambian el ancho de los textos: recalcular al terminar de cargarlas.
  if (document.fonts?.ready) {
    document.fonts.ready.then(scheduleLayout).catch(() => {});
  }
})();
