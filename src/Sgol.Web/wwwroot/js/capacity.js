(() => {
    document.querySelectorAll('[data-capacity-form]').forEach(form => {
        const slider = form.querySelector('[data-capacity-slider]');
        slider.addEventListener('input', () => { form.querySelector('[data-capacity-output]').textContent = `${slider.value}%`; });
    });
    document.querySelector('[data-capacity-preview]')?.focus();
})();
