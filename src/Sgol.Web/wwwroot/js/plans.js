(() => {
    const confirmation = document.querySelector("[data-plan-confirm]");
    if (confirmation) {
        confirmation.close();
        document.querySelector('[data-dialog-open][aria-controls="plan-confirm"]')?.click();
    } else {
        const error = [...document.querySelectorAll("[data-plan-error]")].find(node => node.textContent.trim());
        if (error) error.focus();
        else document.getElementById("plan-result")?.focus();
    }
    const region = document.getElementById("plan-semanal");
    const loading = region?.querySelector("[data-plan-loading]");
    const announce = message => {
        region?.setAttribute("aria-busy", "true");
        if (loading) { loading.textContent = message; loading.hidden = false; }
    };
    region?.querySelectorAll("form").forEach(form => {
        form.addEventListener("submit", () => {
            announce(form.querySelector('button[type="submit"]')?.dataset.progressLabel || "Consultando plan…");
            region.querySelectorAll("button").forEach(button => { button.disabled = true; });
        });
    });
    region?.querySelectorAll("a").forEach(link => {
        link.addEventListener("click", event => {
            if (!event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey)
                announce("Consultando publicaciones…");
        });
    });
})();
