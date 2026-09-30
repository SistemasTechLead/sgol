(() => {
    "use strict";
    document.querySelectorAll("[data-work-query]").forEach(form => {
        form.addEventListener("submit", () => {
            const section = document.getElementById(form.dataset.workQuery);
            section?.setAttribute("aria-busy", "true");
            const body = section?.querySelector("tbody");
            if (!body) return;
            const columns = section.querySelectorAll("thead th").length;
            body.replaceChildren();
            body.classList.add("tabla__cuerpo--cargando");
            body.setAttribute("aria-live", "polite");
            for (let index = 0; index < 3; index++) {
                const row = document.createElement("tr");
                row.className = "tabla__fila-esqueleto";
                const cell = document.createElement("td");
                cell.colSpan = columns;
                const skeleton = document.createElement("span");
                skeleton.className = "esqueleto";
                cell.append(skeleton); row.append(cell); body.append(row);
            }
            const button = form.querySelector('button[type="submit"]');
            if (button) button.disabled = true;
        });
    });
    const script = document.querySelector("script[data-work-focus]");
    const target = document.querySelector("[data-work-error], #session-error") || document.getElementById(script?.dataset.workFocus || "");
    target?.focus();
})();
