(() => {
    "use strict";

    const dialogTriggers = new WeakMap();

    document.addEventListener("click", event => {
        const credentialToggle = event.target.closest("[data-credential-toggle]");
        if (credentialToggle) {
            const input = document.getElementById(credentialToggle.getAttribute("aria-controls"));
            if (input) {
                const reveal = input.type === "password";
                input.type = reveal ? "text" : "password";
                credentialToggle.setAttribute("aria-pressed", reveal ? "true" : "false");
                const label = credentialToggle.querySelector("[data-credential-label]") ?? credentialToggle;
                label.textContent = reveal ? "Ocultar" : "Mostrar";
            }
            return;
        }

        const opener = event.target.closest("[data-dialog-open]");
        if (!opener) {
            return;
        }

        const dialog = document.getElementById(opener.getAttribute("aria-controls"));
        if (dialog instanceof HTMLDialogElement) {
            dialogTriggers.set(dialog, opener);
            dialog.showModal();
            dialog.querySelector("[data-initial-focus]")?.focus();
        }
    });

    document.querySelectorAll("dialog").forEach(dialog => {
        dialog.addEventListener("close", () => dialogTriggers.get(dialog)?.focus());
        if (dialog.id === "navegacion-movil") {
            dialog.addEventListener("keydown", event => {
                if (event.key !== "Tab") return;
                const controls = [...dialog.querySelectorAll('button:not(:disabled), a[href]')]
                    .filter(control => control.getClientRects().length > 0);
                if (!controls.length) return;
                const index = controls.indexOf(document.activeElement);
                const next = (index + (event.shiftKey ? -1 : 1) + controls.length) % controls.length;
                event.preventDefault();
                controls[next].focus();
            });
        }
    });
    document.querySelectorAll('dialog[data-open-on-error="true"]').forEach(dialog => {
        const opener = document.querySelector(`[data-dialog-open][aria-controls="${dialog.id}"]`);
        if (opener) dialogTriggers.set(dialog, opener);
        dialog.showModal();
        dialog.querySelector("[data-dialog-error]")?.focus();
    });
    document.addEventListener("click", event => {
        event.target.closest("[data-dialog-close]")?.closest("dialog")?.close();
    });
    document.querySelectorAll("[data-required-reason]").forEach(field => {
        const error = document.getElementById(field.getAttribute("aria-describedby"));
        field.addEventListener("invalid", () => {
            field.setAttribute("aria-invalid", "true");
            field.closest(".campo")?.classList.add("campo--error");
            if (error) error.hidden = false;
        });
        field.addEventListener("input", () => {
            if (!field.validity.valid) return;
            field.setAttribute("aria-invalid", "false");
            field.closest(".campo")?.classList.remove("campo--error");
            if (error) error.hidden = true;
        });
    });

    document.querySelectorAll("[data-access-form]").forEach(form => {
        form.addEventListener("submit", () => {
            const button = form.querySelector('button[type="submit"]');
            if (button) {
                button.disabled = true;
                button.setAttribute("aria-busy", "true");
                button.textContent = "Verificando…";
            }
        });
    });

    document.querySelectorAll("[data-logout-form]").forEach(form => {
        form.addEventListener("submit", () => {
            form.setAttribute("aria-busy", "true");
            const button = form.querySelector('button[type="submit"]');
            if (button) {
                button.disabled = true;
                button.setAttribute("aria-busy", "true");
                button.textContent = "Cerrando sesión…";
            }
        });
    });

    document.querySelectorAll("[data-progress-form]").forEach(form => {
        form.addEventListener("submit", () => {
            form.setAttribute("aria-busy", "true");
            const button = form.querySelector('button[type="submit"]');
            if (button) {
                button.disabled = true;
                button.setAttribute("aria-busy", "true");
                button.textContent = button.dataset.progressLabel || "Guardando…";
            }
        });
    });

    document.getElementById("acceso-error")?.focus();
    document.getElementById("access-notice")?.focus();
    document.getElementById("session-error")?.focus();
    if (!document.querySelector('dialog[data-open-on-error="true"]'))
        document.getElementById("configuration-error")?.focus();
})();
