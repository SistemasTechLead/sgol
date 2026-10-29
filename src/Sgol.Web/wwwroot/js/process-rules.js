(() => {
    'use strict';
    document.querySelectorAll('[data-process-rules]').forEach(form => {
        const page = form.closest('.process-page') || document;
        const stages = () => Array.from(form.querySelectorAll('[data-process-stage]'));
        const field = (row, key) => row.querySelector(`[name$=".${key}"]`);
        const status = form.querySelector('[data-process-order-status]');
        const startKind = form.querySelector('[data-process-start-kind]');
        const calendar = form.querySelector('[data-process-calendar]');
        const save = page.querySelector('[data-process-save]');
        const hint = page.querySelector('[data-process-save-hint]');
        const saveStatus = page.querySelector('[data-process-save-status]');
        const next = page.querySelector('[data-process-next]');
        const nextText = next?.textContent;
        let dirty = false, leaving = false;
        const flow = form.querySelector('[data-process-stages]');
        const updateFlow = () => {
            if (!flow) return;
            const slots = Array.from(flow.children);
            slots.forEach((slot, index) => {
                const arrow = slot.querySelector('[data-process-flow-arrow]');
                if (!arrow) return;
                const wraps = slots[index + 1] && slots[index + 1].offsetTop > slot.offsetTop + 2;
                arrow.textContent = wraps ? '↵' : '→';
                arrow.title = wraps ? 'Continúa al inicio de la siguiente fila' : 'Siguiente tarea';
                slot.classList.toggle('process-flow-row-end', !!wraps);
            });
        };
        const reveal = control => {
            for (let parent = control.parentElement; parent && parent !== form; parent = parent.parentElement)
                if (parent.tagName === 'DETAILS') parent.open = true;
                else if (parent.tagName === 'DIALOG' && !parent.open) {
                    form.querySelectorAll('dialog[open]').forEach(dialog => dialog.close());
                    parent.showModal();
                }
        };
        const read = () => {
            const value = key => form.elements.namedItem(`Input.${key}`)?.value ?? '';
            return { Name: value('Name'), Description: value('Description'), StartKind: value('StartKind'),
                FirstMonth: value('FirstMonth'), WindowFromDay: value('WindowFromDay'), WindowThroughDay: value('WindowThroughDay'),
                PreferredWeekDay: value('PreferredWeekDay'), StartHour: value('StartHour'),
                Steps: stages().map(row => Object.fromEntries(['TaskId', 'Position', 'Action', 'ReleaseCondition',
                    'MinimumStartOffsetHours', 'ReturnToPosition', 'Instructions', 'OutputItemId', 'OutputTargetTaskId', 'IsFinalAudit'].map(key => [key, field(row, key)?.value ?? '']))) };
        };
        const canonical = input => JSON.stringify([
            String(input.Name ?? '').trim(), String(input.Description ?? '').trim(), input.StartKind,
            ...(input.StartKind === 'SEMESTRAL' ? ['FirstMonth', 'WindowFromDay', 'WindowThroughDay', 'PreferredWeekDay', 'StartHour'].map(k => String(input[k] ?? '')) : []),
            (input.Steps || []).map(row => ['TaskId', 'Position', 'Action', 'ReleaseCondition', 'MinimumStartOffsetHours', 'ReturnToPosition', 'Instructions', 'OutputItemId', 'OutputTargetTaskId', 'IsFinalAudit'].map(k => String(row[k] ?? '').trim()))
        ]);
        let baseline = canonical(read());
        if (form.dataset.processSaved) baseline = canonical(JSON.parse(form.dataset.processSaved));
        const updateDirty = () => {
            dirty = canonical(read()) !== baseline;
            if (save) {
                save.disabled = !dirty;
                save.classList.toggle('boton--primario', dirty);
                save.classList.toggle('boton--secundario', !dirty);
            }
            if (hint) {
                hint.title = dirty ? 'Guardar los cambios del proceso' : 'No hay cambios nuevos';
                hint.setAttribute('aria-label', `Guardar borrador: ${hint.title}`);
                hint.querySelector('[role="tooltip"]').textContent = hint.title;
            }
            if (saveStatus) saveStatus.textContent = dirty ? 'Cambios sin guardar' : form.dataset.processHasDraft === 'true' ? 'Borrador guardado' : 'Versión publicada';
            page.querySelectorAll('[data-process-publish] button').forEach(button => {
                button.disabled = dirty;
                button.title = dirty ? 'Guarda los cambios antes de publicar' : 'Publicar el borrador guardado';
            });
            if (next) next.textContent = dirty ? 'Guarda los cambios antes de publicar. La versión publicada se conserva hasta publicar otro borrador.' : nextText;
            const summary = form.querySelector('[data-process-settings-summary]');
            if (summary) summary.textContent = `${read().Name} · ${startKind.value === 'SEMESTRAL' ? 'Calendario semestral' : 'Ocasión del proceso'}`;
        };
        form.addEventListener('change', event => {
            if (!event.target.matches('[data-output-item]')) return;
            const row = event.target.closest('[data-process-stage]'), ordered = stages();
            const following = ordered[ordered.indexOf(row) + 1];
            field(row, 'OutputTargetTaskId').value = event.target.value && following ? field(following, 'TaskId').value : '';
            updateEntries(); updateDirty();
        });
        const updateEntries = () => {
            const ordered = stages();
            ordered.forEach((row, index) => {
                const item = field(row, 'OutputItemId'), destination = field(row, 'OutputTargetTaskId');
                if (item && destination) {
                    const nextTask = ordered[index + 1];
                    if (!item.value) destination.value = '';
                    // Keep the destination identity after reordering; a changed neighbour requires explicit reselection.
                    row.querySelector('[data-output-description]').textContent = item.value
                        ? `La cantidad reportada calculará la carga de ${nextTask?.querySelector('.process-stage-title strong')?.textContent || 'ninguna tarea: revisa el enlace'}.`
                        : 'Sin conteo de salida. La carga estimada se conserva.';
                    item.setCustomValidity(item.value && (!nextTask || destination.value !== field(nextTask, 'TaskId').value) ? 'El destino inmediato cambió. Vuelve a seleccionar el ítem para confirmar el nuevo enlace.' : '');
                }
                const entry = field(row, 'ReleaseCondition');
                entry.value = index === 0 ? 'INICIO' : field(ordered[index - 1], 'Action').value === 'ENTREGAR' ? 'ENTREGA' : 'APROBACION';
                const target = field(row, 'ReturnToPosition');
                const message = target.value && Number(target.value) >= index + 1 ? 'La devolución debe apuntar a una tarea anterior. Revisa el destino después de mover la tarea.' : '';
                target.setCustomValidity(message);
                const error = row.querySelector('[data-process-stage-error]');
                if (error) error.textContent = message;
                if (message && !form.querySelector('dialog[open]')) reveal(target);
                const badge = row.querySelector('[data-process-position]');
                if (badge) badge.textContent = `Etapa ${index + 1}`;
                const summary = row.querySelector('[data-process-stage-summary]');
                if (summary) summary.textContent = `${field(row, 'Action').selectedOptions[0].textContent} · ${entry.selectedOptions[0].textContent}`;
                const up = row.querySelector('[data-process-up]'), down = row.querySelector('[data-process-down]');
                if (up) up.disabled = index === 0;
                if (down) down.disabled = index === ordered.length - 1;
            });
        };
        const move = (row, destination) => {
            const ordered = stages();
            if (!Number.isInteger(destination) || destination < 1 || destination > ordered.length) {
                field(row, 'Position').value = String(ordered.indexOf(row) + 1); return;
            }
            const returns = new Map(ordered.map(item => [item, ordered[Number(field(item, 'ReturnToPosition').value) - 1]]));
            const openEditor = row.querySelector('[data-process-stage-dialog][open]');
            if (openEditor) openEditor.close();
            ordered.splice(ordered.indexOf(row), 1);
            ordered.splice(destination - 1, 0, row);
            const parent = row.parentElement, anchor = stages().at(-1).nextSibling;
            ordered.forEach(item => parent.insertBefore(item, anchor));
            ordered.forEach((item, index) => {
                field(item, 'Position').value = String(index + 1);
                if (returns.get(item)) field(item, 'ReturnToPosition').value = String(ordered.indexOf(returns.get(item)) + 1);
                item.querySelectorAll('[name], [id], label[for]').forEach(control => {
                    for (const attribute of ['name', 'id', 'for']) {
                        if (control.hasAttribute(attribute)) control.setAttribute(attribute, control.getAttribute(attribute)
                            .replace(/Input\.Steps\[\d+\]/g, `Input.Steps[${index}]`)
                            .replace(/Input_Steps_\d+__/g, `Input_Steps_${index}__`));
                    }
                });
            });
            updateEntries(); updateDirty();
            if (openEditor && !form.querySelector('dialog[open]')) openEditor.showModal();
            updateFlow();
            if (status) status.textContent = `Tarea movida a la posición ${destination}.`;
        };
        const updateCalendar = () => {
            if (!calendar || !startKind) return;
            calendar.hidden = startKind.value !== 'SEMESTRAL';
            calendar.querySelectorAll('input, select, textarea').forEach(control => control.disabled = calendar.hidden);
        };
        let dragging = null;
        const clearDrop = () => {
            stages().forEach(row => row.classList.remove('process-drop-before', 'process-drop-after', 'process-dragging'));
            form.querySelector('[data-process-add-slot]')?.classList.remove('process-drop-before');
        };
        stages().forEach(row => {
            const position = field(row, 'Position');
            position.max = String(stages().length);
            position.addEventListener('change', () => move(row, Number(position.value)));
            row.querySelector('[data-process-up]')?.addEventListener('click', () => { move(row, stages().indexOf(row)); row.querySelector('[data-process-edit]')?.focus(); });
            row.querySelector('[data-process-down]')?.addEventListener('click', () => { move(row, stages().indexOf(row) + 2); row.querySelector('[data-process-edit]')?.focus(); });
            const editor = row.querySelector('[data-process-stage-dialog]');
            editor.addEventListener('keydown', event => {
                if (event.key !== 'Tab') return;
                const controls = Array.from(editor.querySelectorAll('button,input,select,textarea,summary,[tabindex]')).filter(el => !el.disabled && el.tabIndex >= 0 && el.getClientRects().length);
                const first = controls[0], last = controls[controls.length - 1];
                if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
                else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
            });
            row.querySelector('[data-process-edit]')?.addEventListener('click', () => editor.showModal());
            row.querySelector('[data-process-edit-close]')?.addEventListener('click', () => editor.close());
            row.querySelector('[data-process-drag]')?.addEventListener('dragstart', event => {
                dragging = row; event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('text/plain', field(row, 'TaskId').value);
                event.dataTransfer.setDragImage(row, 20, 20); row.classList.add('process-dragging');
            });
            row.addEventListener('dragover', event => {
                if (!dragging || dragging === row) return;
                event.preventDefault(); event.dataTransfer.dropEffect = 'move';
                clearDrop(); dragging.classList.add('process-dragging');
                const bounds = row.getBoundingClientRect();
                const horizontal = flow && getComputedStyle(flow).display === 'grid' && getComputedStyle(flow).gridTemplateColumns.split(' ').length > 1;
                row.classList.add((horizontal ? event.clientX < bounds.left + bounds.width / 2 : event.clientY < bounds.top + bounds.height / 2) ? 'process-drop-before' : 'process-drop-after');
            });
            row.addEventListener('drop', event => {
                if (!dragging) return;
                event.preventDefault();
                const others = stages().filter(item => item !== dragging);
                if (row !== dragging) move(dragging, others.indexOf(row) + (row.classList.contains('process-drop-after') ? 2 : 1));
                clearDrop(); dragging = null;
            });
            row.addEventListener('dragend', () => { clearDrop(); dragging = null; });
        });
        const addSlot = form.querySelector('[data-process-add-slot]');
        addSlot?.addEventListener('dragover', event => {
            if (!dragging) return;
            event.preventDefault(); clearDrop(); addSlot.classList.add('process-drop-before');
        });
        addSlot?.addEventListener('dragleave', () => addSlot.classList.remove('process-drop-before'));
        addSlot?.addEventListener('drop', event => {
            if (!dragging) return;
            event.preventDefault(); move(dragging, stages().length);
            clearDrop(); addSlot.classList.remove('process-drop-before'); dragging = null;
        });
        form.addEventListener('input', () => { updateCalendar(); updateEntries(); updateDirty(); });
        form.addEventListener('change', () => { updateCalendar(); updateEntries(); updateDirty(); });
        form.addEventListener('invalid', event => reveal(event.target), true);
        form.addEventListener('submit', event => {
            updateEntries(); updateDirty();
            if (!dirty || !form.reportValidity()) { event.preventDefault(); return; }
            leaving = true;
        });
        // External submissions reload the editor; never silently discard its local edits.
        page.querySelectorAll('form').forEach(other => {
            if (other === form) return;
            other.addEventListener('submit', event => {
                if (dirty && (other.matches('[data-process-publish]') || !window.confirm('Hay cambios sin guardar. ¿Quieres descartarlos y continuar?'))) {
                    event.preventDefault(); return;
                }
                leaving = true;
            });
        });
        window.addEventListener('pageshow', () => { leaving = false; });
        window.addEventListener('beforeunload', event => {
            if (dirty && !leaving) { event.preventDefault(); event.returnValue = ''; }
        });
        const picker = page.querySelector('[data-process-add-dialog]');
        page.querySelector('[data-process-add-open]')?.addEventListener('click', () => picker.showModal());
        page.querySelector('[data-process-add-close]')?.addEventListener('click', () => picker.close());
        if (picker && new URLSearchParams(location.search).has('Search')) picker.showModal();
        updateCalendar(); updateEntries(); updateDirty();
        updateFlow();
        if (flow) new ResizeObserver(updateFlow).observe(flow);
        if (form.dataset.processErrors === 'true') {
            form.querySelectorAll('details').forEach(detail => detail.open = true);
            const invalid = form.querySelector('.input-validation-error, input:invalid, select:invalid, textarea:invalid');
            if (invalid) reveal(invalid);
        }
    });
})();
