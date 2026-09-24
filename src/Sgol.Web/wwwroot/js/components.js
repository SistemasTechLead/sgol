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
                credentialToggle.textContent = reveal ? "Ocultar" : "Mostrar";
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
        }
    });

    document.querySelectorAll("dialog").forEach(dialog => {
        dialog.addEventListener("close", () => dialogTriggers.get(dialog)?.focus());
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

    document.getElementById("acceso-error")?.focus();
    document.getElementById("access-notice")?.focus();
    document.getElementById("session-error")?.focus();
})();
