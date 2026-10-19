(() => {
    const form = document.querySelector('[data-occasional-create]');
    if (!form) return;

    const requirement = form.querySelector('[data-occasional-evidence]');
    const updatePhotos = () => {
        const requiresPhotos = ['FOTOGRAFIA', 'CAPTURA_PANTALLA'].includes(requirement.value);
        form.querySelectorAll('[data-occasional-photos]').forEach(field => {
            field.hidden = !requiresPhotos;
            field.querySelector('input').disabled = !requiresPhotos;
        });
    };

    requirement.addEventListener('change', updatePhotos);
    form.addEventListener('reset', () => setTimeout(updatePhotos, 0));
    updatePhotos();
})();
