(() => {
    document.querySelectorAll('[data-correction-tomorrow]').forEach(button => button.addEventListener('click', () => {
        const section = button.closest('[data-correction-schedule]');
        section.querySelectorAll('input[type="date"]').forEach(input => { input.value = button.dataset.correctionTomorrow; });
        section.querySelector('input').dispatchEvent(new Event('change', { bubbles: true }));
    }));
    const update = () => document.querySelectorAll('[data-correction-schedule]').forEach(section => {
        const form = section.closest('form');
        const card = section.closest('[data-validation-card]');
        const action = card?.querySelector('select[name$=".Action"]') || form.querySelector('[data-review-action]');
        const negative = card ? card.querySelector('[data-validation-choice]:checked')?.value === 'no' : form.querySelector('[name="result"]:checked')?.value === 'no';
        const enabled = form.hasAttribute('data-correction-reschedule') || negative && action?.value !== 'NO_SATISFECHA';
        section.hidden = !enabled;
        const custom = section.querySelector('[data-correction-mode]').value === 'CUSTOM';
        section.querySelector('[data-correction-dates]').hidden = !custom;
        section.querySelectorAll('input, select').forEach(control => {
            control.disabled = !enabled || control.matches('input[type="date"]') && !custom;
            if (control.name.endsWith('.From')) control.required = enabled && custom;
        });
    });
    document.addEventListener('change', event => {
        update();
        const form = event.target.closest('form');
        form?.querySelectorAll('[data-correction-apply]').forEach(button => { button.disabled = true; });
        const preview = form?.querySelector('[data-correction-preview]');
        if (preview) preview.setAttribute('aria-label', 'Propuesta modificada: calcula otra vista previa');
    });
    document.addEventListener('input', event => event.target.closest('form')?.querySelectorAll('[data-correction-apply]').forEach(button => { button.disabled = true; }));
    window.addEventListener('pageshow', () => queueMicrotask(update));
    // Other form controllers initialize first; hidden controls must remain disabled afterwards.
    queueMicrotask(update);
    document.addEventListener('DOMContentLoaded', () => (document.querySelector('dialog[open] [data-correction-error], dialog[open] [data-correction-preview]') || document.querySelector('[data-correction-error], [data-correction-preview]'))?.focus());
})();
