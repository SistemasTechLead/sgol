(() => {
    "use strict";
    const prepared = document.querySelector('dialog[data-correction-prepared="true"]');
    if (prepared) {
        // Use the shared opener so Escape and Cancel restore the same trigger.
        document.querySelector(`[data-dialog-open][aria-controls="${prepared.id}"]`)?.click();
    } else {
        document.querySelector("[data-assignment-error]")?.focus();
    }
    document.querySelectorAll("[data-assignment-read]").forEach(form => {
        form.addEventListener("submit", () => {
            const section = document.getElementById(form.dataset.assignmentRead);
            if (section) section.setAttribute("aria-busy", "true");
            const loading = document.querySelector(`[data-assignment-loading="${form.dataset.assignmentRead}"]`);
            if (loading) loading.hidden = false;
        });
    });
})();
