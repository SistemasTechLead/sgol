(() => {
    const form = document.querySelector('[data-incidence-form]');
    if (!form) return;
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
