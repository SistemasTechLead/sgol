(() => {
    document.querySelectorAll('.supervision-filters').forEach(form => {
        const range = form.querySelector('select[name$=".range"]');
        const dates = form.querySelector('.supervision-custom');
        range.addEventListener('change', () => { dates.open = range.value === 'CUSTOM'; });
        form.querySelectorAll('input[type="date"]').forEach(input => input.addEventListener('change', () => { range.value = 'CUSTOM'; }));
    });
    const dialog = document.querySelector('[data-supervision-detail]');
    if (dialog) {
        dialog.showModal();
        dialog.addEventListener('close', () => {
            const url = new URL(location.href);
            url.searchParams.delete('task');
            history.replaceState(null, '', url);
        });
    }
    document.querySelectorAll('[data-supervision-review]').forEach(form => {
        const save = form.querySelector('[data-review-save]');
        let submitting = false;
        form.addEventListener('submit', event => {
            if (submitting) { event.preventDefault(); return; }
            submitting = true;
            form.setAttribute('aria-busy', 'true');
            save.disabled = true;
            save.textContent = 'Procesando…';
        });
        window.addEventListener('pageshow', () => {
            submitting = false;
            form.removeAttribute('aria-busy');
            save.disabled = false;
            save.textContent = 'Validar';
        });
        const update = () => {
            const negative = form.querySelector('[name="result"]:checked').value === 'no';
            form.querySelector('[data-review-negative]').hidden = !negative;
            form.querySelector('[data-review-comment]').required = negative;
            form.querySelector('[data-review-decision]').value = negative ? form.querySelector('[data-review-action]').value : 'CONCLUIDA';
        };
        form.addEventListener('change', update);
        update();
    });
})();
