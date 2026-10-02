(() => {
    "use strict";
    document.querySelector('[data-continuity-cancelled="true"]')?.focus();
    document.querySelector('[data-work-query="continuity"]')?.addEventListener("submit", event => {
        event.target.querySelector('button[type="submit"]').disabled = true;
    });
    const content = document.querySelector("[data-continuity-confirm]");
    if (!content) return;
    const dialog = document.createElement("dialog");
    dialog.className = "modal";
    dialog.setAttribute("aria-label", document.querySelector("[data-continuity-confirm]").closest("section").querySelector("h2").textContent);
    content.before(dialog); dialog.append(content); dialog.showModal();
    content.querySelector("[data-initial-focus]").focus();
    dialog.addEventListener("cancel", event => { event.preventDefault(); if (dialog.getAttribute("aria-busy") !== "true") content.querySelector("[data-initial-focus]").click(); });
    content.querySelector("[data-continuity-send]").addEventListener("submit", () => {
        dialog.setAttribute("aria-busy", "true");
        const status = document.createElement("p"); status.setAttribute("role", "status");
        status.textContent = content.querySelector('button[type="submit"].boton--primario').textContent.includes("Aprobar") ? "Registrando aprobación…" : "Enviando solicitud…";
        content.append(status);
        content.querySelectorAll("button").forEach(button => { button.disabled = true; });
    });
})();
