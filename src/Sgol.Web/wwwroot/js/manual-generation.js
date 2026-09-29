(() => {
    "use strict";
    const dialog = document.getElementById("manual-confirm");
    if (dialog?.dataset.manualOpen === "true") document.getElementById("manual-open-confirm")?.click();
    const form = document.getElementById("manual-form");
    const submit = form?.querySelector('button[type="submit"]');
    const blocked = submit?.disabled;
    const validity = () => { if (submit) submit.disabled = blocked || !!form.querySelector(":invalid"); };
    form?.addEventListener("input", validity);
    form?.addEventListener("change", validity);
    validity();
    const list = document.getElementById("manual-claimants");
    const add = document.getElementById("add-claimant");
    let serial = list?.children.length || 0;
    const update = () => {
        const rows = list.querySelectorAll("[data-claimant]");
        rows.forEach((row, index) => {
            row.querySelector("label").textContent = `Reclamante ${index + 1}`;
            const remove = row.querySelector("button");
            remove.textContent = `Quitar reclamante ${index + 1}`;
            remove.disabled = rows.length <= 2;
        });
        add.disabled = rows.length >= 20;
        validity();
    };
    add?.addEventListener("click", () => {
        if (list.children.length >= 20) return;
        const row = list.firstElementChild.cloneNode(true);
        const input = row.querySelector("input");
        input.id = `claimant-${serial++}`;
        input.value = "";
        row.querySelector("label").htmlFor = input.id;
        list.append(row);
        update();
        input.focus();
    });
    list?.addEventListener("click", event => {
        const button = event.target.closest("[data-remove-claimant]");
        if (!button || list.children.length <= 2) return;
        const row = button.closest("[data-claimant]");
        const target = row.previousElementSibling?.querySelector("input") || add;
        row.remove();
        update();
        target.focus();
    });
    const parent = document.getElementById("manual-parentObligationId");
    const showPeriod = () => {
        document.getElementById("parent-period").textContent = parent.selectedOptions[0]?.dataset.period
            ? `Período heredado: ${parent.selectedOptions[0].dataset.period}`
            : "El período se hereda de la recepción seleccionada.";
    };
    parent?.addEventListener("change", showPeriod);
    if (parent) showPeriod();
    if (!dialog?.open) (document.getElementById("manual-error") || document.getElementById("manual-result"))?.focus();
})();