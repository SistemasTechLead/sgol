(() => {
    "use strict";
    document.querySelectorAll("[data-work-query]").forEach(form => {
        form.addEventListener("submit", () => {
            const section = document.getElementById(form.dataset.workQuery);
            section?.setAttribute("aria-busy", "true");
            const status = section?.querySelector("[data-front-query-status]"); if (status) status.hidden = false;
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
    const target = document.querySelector("[data-work-error], [data-front-error], [data-front-success], #session-error") || document.getElementById(script?.dataset.workFocus || "");
    // Restore focus after the browser has navigated to the submitted fragment.
    if (document.readyState === "complete") target?.focus();
    else window.addEventListener("load", () => target?.focus(), { once: true });
    document.querySelectorAll("[data-front-confirm]").forEach(form => {
        const submit = form.querySelector("button[type=submit]");
        const actions = form.querySelector(".acciones-formulario");
        const opener = document.createElement("button");
        opener.type = "button"; opener.className = "boton boton--primario"; opener.textContent = submit.textContent;
        const dialog = document.createElement("dialog"); dialog.className = "modal";
        const title = document.createElement("h2"); title.id = "confirm-" + form.dataset.frontConfirm.replaceAll(" ", "-"); title.textContent = form.dataset.frontConfirm;
        dialog.setAttribute("aria-labelledby", title.id); dialog.setAttribute("aria-modal", "true");
        const content = document.createElement("div"); content.className = "modal__contenido"; title.className = "modal__titulo"; content.append(title);
        form.querySelectorAll(":scope > p").forEach(p => { p.className = "modal__texto"; content.append(p); });
        form.querySelectorAll(":scope > label").forEach(label => content.append(label));
        const cancel = document.createElement("button"); cancel.type = "button"; cancel.className = "boton boton--secundario"; cancel.textContent = "Cancelar";
        actions.className = "modal__acciones"; actions.replaceChildren(cancel, submit); content.append(actions); dialog.append(content); form.append(opener, dialog);
        opener.addEventListener("click", () => { dialog.showModal(); cancel.focus(); });
        cancel.addEventListener("click", () => dialog.close());
        dialog.addEventListener("cancel", event => { if (form.getAttribute("aria-busy") === "true") event.preventDefault(); });
        dialog.addEventListener("close", () => opener.focus());
        form.addEventListener("submit", () => {
            form.setAttribute("aria-busy", "true"); submit.disabled = cancel.disabled = opener.disabled = true;
            title.textContent = form.dataset.frontBusy; title.setAttribute("role", "status");
        });
        if (!document.querySelector("[data-front-error]")) { dialog.showModal(); cancel.focus(); }
    });
    document.querySelectorAll("[data-front-query]").forEach(form => form.addEventListener("submit", () => {
        form.setAttribute("aria-busy", "true"); form.querySelector("button[type=submit]").disabled = true;
        const status = form.querySelector("[data-front-query-status]"); if (status) status.hidden = false;
    }));
})();
