document.querySelectorAll(".scenario-form").forEach((form) => {
    form.addEventListener("submit", () => {
        const button = form.querySelector("[data-run-button]");
        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        button.querySelector("[data-normal-label]").hidden = true;
        button.querySelector("[data-loading-label]").hidden = false;
    });
});
