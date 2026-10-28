(() => {
    'use strict';
    const options = document.querySelector('[data-refresh-options]');
    const apply = document.querySelector('[data-refresh-apply]');
    if (!options) return;
    options.addEventListener('change', event => {
        if (!event.target.matches('input[type="checkbox"]')) return;
        const choice = event.target.closest('label');
        choice?.classList.toggle('assignment-refresh__choice--accepted', event.target.checked);
        const status = choice?.querySelector('[data-refresh-choice-status]');
        if (status) status.textContent = event.target.checked ? 'Seleccionado; pulsa Revisar cambios' : 'Marca esta opción para continuar';
        options.querySelector('[data-refresh-dirty]').hidden = false;
        if (apply) apply.querySelector('button[type="submit"]').disabled = true;
    });
    options.addEventListener('submit', () => {
        const reason = apply?.querySelector('textarea[name="Reason"]');
        if (reason) options.querySelector('input[name="Reason"]').value = reason.value;
    });
})();

