(() => {
    const form = document.querySelector('[data-validation-batch]');
    if (!form) return;
    const cards = [...form.querySelectorAll('[data-validation-card]')];
    const save = form.querySelector('[data-validation-save]');
    let submitting = false;
    const update = () => {
        let count = 0;
        cards.forEach(card => {
            const choice = card.querySelector('[data-validation-choice]:checked');
            const negative = card.querySelector('[data-validation-negative]');
            if (!negative) return;
            if (choice) count++;
            card.classList.toggle('validation-card--selected', Boolean(choice));
            negative.hidden = choice?.value !== 'no';
            negative.querySelectorAll('textarea, select').forEach(control => {
                control.disabled = negative.hidden;
                if (control.tagName === 'TEXTAREA') control.required = !negative.hidden;
            });
        });
        save.disabled = count === 0 || count > 500 || submitting;
        form.querySelector('[data-validation-count]').textContent = count ? `${count} tareas seleccionadas${count > 500 ? ' · máximo 500 por guardado' : ''}` : 'Selecciona las tareas que deseas revisar.';
    };
    form.addEventListener('change', event => {
        if (event.target.matches('[data-validation-choice]') && event.target.checked)
            event.target.closest('[data-validation-card]').querySelectorAll('[data-validation-choice]').forEach(other => {
                if (other !== event.target) other.checked = false;
            });
        update();
    });
    form.addEventListener('submit', event => {
        if (submitting || save.disabled) { event.preventDefault(); return; }
        submitting = true;
        cards.forEach(card => {
            if (!card.querySelector('[data-validation-choice]:checked'))
                card.querySelectorAll('input, textarea, select').forEach(control => { control.disabled = true; });
        });
        save.textContent = 'Guardando revisión…';
        update();
    });
    window.addEventListener('pageshow', () => {
        submitting = false;
        cards.forEach(card => card.querySelectorAll('input').forEach(control => { control.disabled = false; }));
        save.textContent = 'Guardar Revisión'; update();
    });
    document.querySelector('[data-validation-section]')?.addEventListener('change', event => {
        if (cards.some(card => card.querySelector('[data-validation-choice]:checked')) && !confirm('Hay revisiones sin guardar. ¿Deseas cambiar de apartado y descartarlas?')) {
            event.target.value = form.elements.Section.value;
            return;
        }
        event.target.form.requestSubmit();
    });
    document.querySelectorAll('[data-validation-gallery]').forEach(gallery => {
        const photos = [...gallery.querySelectorAll('[data-validation-photo]')];
        if (!photos.length) return;
        let current = 0;
        const showPhoto = () => {
            photos.forEach((photo, index) => { photo.hidden = index !== current; });
            const count = gallery.querySelector('[data-photo-count]');
            if (count) count.textContent = `${current + 1} / ${photos.length}`;
        };
        showPhoto();
        gallery.querySelectorAll('[data-photo-step]').forEach(button => button.addEventListener('click', () => {
            current = (current + Number(button.dataset.photoStep) + photos.length) % photos.length;
            showPhoto();
        }));
    });
    window.addEventListener('beforeunload', event => {
        if (!submitting && cards.some(card => card.querySelector('[data-validation-choice]:checked'))) event.preventDefault();
    });
    update();
})();
