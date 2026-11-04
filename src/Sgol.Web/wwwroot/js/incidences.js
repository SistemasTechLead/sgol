(() => {
    const form = document.querySelector('[data-incidence-form]');
    if (!form) return;
    form.querySelectorAll('.incidence-task--decision').forEach(task => {
        const action = task.querySelector('select[name$=".Action"]');
        const options = task.querySelector('[data-incidence-transfer]');
        if (!action || !options) return;
        const update = () => {
            const transfer = action.value === 'TRANSFER';
            options.hidden = !transfer;
            options.disabled = !transfer;
            if (!transfer) {
                options.querySelectorAll('select').forEach(select => { select.value = ''; });
                options.querySelectorAll('input[type="checkbox"]').forEach(input => { input.checked = false; });
            }
        };
        action.addEventListener('change', update);
        update();
    });
    form.addEventListener('submit', () => {
        form.setAttribute('aria-busy', 'true');
        form.querySelector('[data-incidence-loading]').hidden = false;
    });
    const confirm = form.querySelector('[data-incidence-confirm]');
    if (!confirm) return;
    const status = form.querySelector('[data-incidence-stale]');
    form.addEventListener('input', event => {
        if (!(event.target instanceof HTMLElement) || event.target.getAttribute('type') === 'hidden') return;
        confirm.disabled = true;
        status.hidden = false;
    });
})();
