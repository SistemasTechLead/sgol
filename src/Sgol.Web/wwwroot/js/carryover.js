(() => {
    'use strict';
    const form = document.querySelector('[data-postponement]');
    if (!form) return;
    const boxes = [...form.querySelectorAll('input[name="Selected"]:not(:disabled)')];
    const all = form.querySelector('[data-select-all]');
    const update = () => {
        const count = boxes.filter(box => box.checked).length;
        form.querySelector('[data-selected-count]').textContent = `${count} tareas seleccionadas`;
        boxes.forEach(box => box.closest('tr').classList.toggle('carryover-selected', box.checked));
        if (all) { all.checked = count > 0 && count === boxes.length; all.indeterminate = count > 0 && count < boxes.length; }
        form.querySelectorAll('button').forEach(button => { if (!button.dataset.stale) button.disabled = count === 0; });
    };
    const invalidate = () => {
        const submit = form.querySelector('[data-postponement-submit]');
        if (submit) { submit.disabled = true; submit.dataset.stale = 'true'; form.querySelector('[data-selection-changed]').hidden = false; }
    };
    form.addEventListener('input', invalidate);
    form.addEventListener('change', update);
    all?.addEventListener('change', () => { boxes.forEach(box => { box.checked = all.checked; }); invalidate(); update(); });
    boxes.forEach(box => box.closest('tr').addEventListener('click', event => {
        if (event.target.closest('input, label, a, button')) return;
        box.checked = !box.checked; invalidate(); update();
    }));
    update();
})();
