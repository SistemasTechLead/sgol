(() => {
    document.querySelectorAll('[data-item-quantity]').forEach(input => {
        if (input.dataset.itemReady) return;
        input.dataset.itemReady = 'true';
        const formula = input.form.querySelector('[data-item-formula]');
        const update = () => {
            const quantity = Number(input.value), minutes = Number(input.dataset.itemMinutes);
            formula.textContent = input.value !== '' && Number.isSafeInteger(quantity) && quantity >= 0
                ? quantity === 0 ? 'Cero: omitir tareas intermedias y pasar a auditoría final, conservando su carga y entregable.'
                    : `${quantity} × ${minutes} min = ${(BigInt(quantity) * BigInt(minutes)).toString()} min de carga siguiente.`
                : 'Indica una cantidad entera. Vacío no significa cero.';
        };
        input.addEventListener('input', update); update();
    });
    document.querySelector('[data-item-conflict]')?.focus();
})();
