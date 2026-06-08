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
