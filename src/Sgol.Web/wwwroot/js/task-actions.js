(() => {
    'use strict';
    const catalog = document.querySelector('[data-task-catalog]');
    const panel = catalog?.querySelector('.task-action-panel');
    if (!panel) return;
    const rows = [...catalog.querySelectorAll('[data-task-row]')];
    const edit = panel.querySelector('[data-task-edit]');
    const toggle = panel.querySelector('[data-task-toggle]');
    const assign = panel.querySelector('[data-task-assign]');
    let selected = null;

    function select(row) {
        selected = row;
        rows.forEach(item => {
            const active = item === row;
            item.classList.toggle('task-row--selected', active);
            item.querySelector('.task-selection').checked = active;
        });
        const task = row.dataset;
        panel.querySelector('[data-task-selection-label]').textContent = `${task.taskCode} · ${task.taskName}`;
        panel.querySelector('[data-task-selection-hint]').textContent = task.taskAssignable === 'true'
            ? 'Tarea ocasional activa.' : 'La asignación ocasional requiere una tarea ocasional activa del proceso General.';
        catalog.querySelector('[data-task-delete-name]').textContent = `${task.taskCode} · ${task.taskName}`;
        catalog.querySelectorAll('[data-task-change]').forEach(form => {
            form.elements.id.value = task.taskRow;
            form.elements.revision.value = task.taskRevision;
            form.querySelectorAll('input, button').forEach(control => { control.disabled = false; });
        });
        edit.disabled = false;
        panel.querySelector('[data-task-delete]').disabled = false;
        toggle.textContent = task.taskActive === 'true' ? 'Desactivar' : 'Activar';
        assign.disabled = task.taskAssignable !== 'true';
    }

    rows.forEach(row => {
        row.addEventListener('click', event => {
            if (event.target.closest('a, button, details, label, select, textarea, input:not(.task-selection)')) return;
            select(row);
        });
        row.querySelector('.task-selection').addEventListener('change', () => select(row));
    });
    catalog.querySelectorAll('[data-task-change]').forEach(form => {
        form.addEventListener('submit', event => { if (!selected) event.preventDefault(); });
    });
    edit.addEventListener('click', () => {
        if (selected) window.location.assign(`/tareas?edit=${encodeURIComponent(selected.dataset.taskRow)}`);
    });
    assign.addEventListener('click', () => {
        if (selected?.dataset.taskAssignable === 'true')
            window.location.assign(`/tareas/asignar?taskId=${encodeURIComponent(selected.dataset.taskRow)}`);
    });
    // Reloads and back navigation always begin without an implicit selection.
    window.addEventListener('pageshow', () => {
        selected = null;
        rows.forEach(row => {
            row.classList.remove('task-row--selected');
            row.querySelector('.task-selection').checked = false;
        });
        panel.querySelectorAll('button, input').forEach(control => { control.disabled = true; });
        catalog.querySelectorAll('[data-task-change] input, [data-task-delete-confirm]').forEach(control => { control.disabled = true; });
        panel.querySelector('[data-task-selection-label]').textContent = 'Ninguna tarea seleccionada';
        panel.querySelector('[data-task-selection-hint]').textContent = 'Selecciona una tarea para habilitar las acciones.';
        toggle.textContent = 'Activar / Desactivar';
        catalog.querySelector('#task-delete').close();
    });
})();
