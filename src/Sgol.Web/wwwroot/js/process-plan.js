(() => {
    'use strict';
    const page = document.querySelector('[data-process-plan-page]');
    if (!page) return;
    page.querySelectorAll('[data-process-plan-flow]').forEach(flow => {
        const update = () => {
            const cards = Array.from(flow.children);
            cards.forEach((card, index) => {
                const arrow = card.querySelector('[data-process-flow-arrow]');
                if (!arrow) return;
                arrow.hidden = index === cards.length - 1;
                const wraps = cards[index + 1] && cards[index + 1].offsetTop > card.offsetTop + 2;
                card.classList.toggle('process-flow-row-end', !!wraps);
                arrow.textContent = wraps ? '↵' : '→';
                arrow.title = wraps ? 'Continúa al inicio de la siguiente fila' : 'Siguiente tarea';
            });
        };
        update(); new ResizeObserver(update).observe(flow);
    });
    page.querySelectorAll('[data-process-plan-detail-open]').forEach(button => {
        const dialog = button.parentElement.querySelector('dialog');
        button.addEventListener('click', () => dialog.showModal());
        dialog.querySelector('[data-process-plan-detail-close]').addEventListener('click', () => dialog.close());
    });
    const editor = page.querySelector('[data-process-plan-editor]');
    if (!editor) return;
    const snapshot = () => JSON.stringify(Array.from(new FormData(editor)));
    const baseline = snapshot();
    const reviewed = editor.dataset.hasPreview === 'true';
    const save = page.querySelector('[data-process-plan-save]');
    const status = page.querySelector('[data-process-plan-status]');
    const next = page.querySelector('[data-process-plan-next]');
    const nextText = next.textContent;
    let dirty = false, leaving = false;
    const update = () => {
        dirty = snapshot() !== baseline;
        const selects = Array.from(editor.querySelectorAll('[data-process-plan-person]'));
        const assigned = selects.filter(select => select.value).length;
        const automatic = selects.filter(select => select.value === '00000000-0000-0000-0000-000000000000').length;
        save.disabled = !reviewed || dirty;
        save.title = dirty ? 'Revisa las fechas y la capacidad después de los cambios' : reviewed ? 'Guardar los responsables verificados' : 'Primero revisa las fechas y la capacidad';
        status.textContent = reviewed && !dirty ? 'Fechas y capacidad verificadas · siguiente paso: guardar' : `${assigned} de ${selects.length} responsables seleccionados · ${automatic} en Aleatorio`;
        next.textContent = reviewed && dirty ? 'El plan cambió. Vuelve a revisar fechas y capacidad antes de guardar.' : nextText;
        page.querySelector('[data-process-plan-preview]')?.classList.toggle('process-plan-preview-stale', dirty);
    };
    editor.addEventListener('input', update); editor.addEventListener('change', update);
    editor.addEventListener('invalid', event => {
        for (let parent = event.target.parentElement; parent && parent !== editor; parent = parent.parentElement)
            if (parent.tagName === 'DETAILS') parent.open = true;
    }, true);
    page.querySelector('[data-process-plan-auto]')?.addEventListener('click', () => {
        editor.querySelectorAll('[data-process-plan-person]').forEach(select => select.value = '00000000-0000-0000-0000-000000000000');
        update();
    });
    editor.addEventListener('submit', () => { leaving = true; });
    page.querySelector('#process-plan-prepare')?.addEventListener('submit', event => {
        if (dirty || !reviewed) { event.preventDefault(); return; }
        leaving = true;
    });
    window.addEventListener('pageshow', () => { leaving = false; });
    window.addEventListener('beforeunload', event => {
        if (dirty && !leaving) { event.preventDefault(); event.returnValue = ''; }
    });
    update();
})();
