(() => {
    'use strict';
    document.querySelectorAll('[data-checklist-editor]').forEach(root => {
        const form = root.closest('form'), requirement = form.querySelector('[name="Input.Requirement"]');
        const hidden = root.querySelector('[data-checklist-definition]'), rows = root.querySelector('[data-checklist-rows]');
        let points;
        try { points = JSON.parse(hidden.value || '[]'); } catch { points = []; }
        const sync = () => { hidden.value = JSON.stringify(points.filter(p => p.Text.trim())); };
        const render = () => {
            rows.replaceChildren();
            const addRow = (point, index, empty) => {
                const row = document.createElement('div'); row.className = 'checklist-row' + (empty ? ' checklist-row--empty' : '');
                const input = document.createElement('textarea'); input.className = 'campo__control'; input.rows = 2; input.maxLength = 1000;
                input.value = point.Text; input.setAttribute('aria-label', empty ? 'Agregar otro punto' : `Punto ${index + 1}`);
                input.placeholder = empty ? 'Agregar otro punto…' : 'Describe la comprobación';
                input.addEventListener('input', () => {
                    point.Text = input.value;
                    if (empty && input.value.trim()) {
                        empty = false; points.push(point); row.classList.remove('checklist-row--empty');
                        input.setAttribute('aria-label', `Punto ${points.length}`); addActions(row, index); addRow({Id: crypto.randomUUID(), Text: ''}, points.length, true);
                        rows.querySelectorAll('[data-checklist-down]').forEach((b,i)=>b.disabled=i>=points.length-1);
                    }
                    sync();
                });
                row.append(input); if (!empty) addActions(row, index); rows.append(row);
            };
            const addActions = (row, index) => {
                const actions = document.createElement('div'); actions.className = 'checklist-row-actions';
                [['↑','Subir punto',-1],['↓','Bajar punto',1],['Eliminar','Eliminar punto',0]].forEach(([label,title,offset]) => {
                    const button = document.createElement('button'); button.type = 'button'; button.className = 'boton boton--secundario'; button.textContent = label;
                    button.setAttribute('aria-label', `${title} ${index + 1}`);
                    if(offset===1) button.dataset.checklistDown='';
                    button.disabled = offset !== 0 && (index + offset < 0 || index + offset >= points.length);
                    button.addEventListener('click', () => {
                        if (!offset) points.splice(index, 1);
                        else [points[index],points[index+offset]] = [points[index+offset],points[index]];
                        sync(); render(); rows.querySelectorAll('textarea')[Math.min(index,points.length)]?.focus();
                    }); actions.append(button);
                }); row.append(actions);
            };
            points.forEach((p,i) => addRow(p,i,false)); addRow({Id:crypto.randomUUID(),Text:''},points.length,true);
        };
        const visibility = () => { root.hidden = requirement.value !== 'CHECKLIST'; };
        requirement.addEventListener('change', visibility); visibility(); render();
        form.addEventListener('submit', e => {
            sync();
            if (requirement.value === 'CHECKLIST' && !points.some(p => p.Text.trim())) {
                e.preventDefault(); const input=rows.querySelector('textarea'); input.setCustomValidity('Agrega al menos un punto.'); input.reportValidity();
                input.addEventListener('input',()=>input.setCustomValidity(''),{once:true});
            }
        });
    });
    document.querySelectorAll('[data-checklist-evidence][data-readonly="false"]').forEach(root => {
        const form = root.closest('form'), boxes = [...root.querySelectorAll('[data-check-id]')];
        const answer = root.querySelector('[data-checklist-answer]'), count=root.querySelector('[data-checklist-count]'), status=root.querySelector('[data-checklist-save]');
        const buttons = [...form.querySelectorAll('button[type="submit"]')].filter(b => b.value !== 'DEVOLUCION');
        let queue=Promise.resolve(), timer, ready=false, waiting=false;
        const update = () => {
            const checked=boxes.filter(b=>b.checked).map(b=>b.dataset.checkId);
            answer.value=JSON.stringify({Definition:root.dataset.definition, Checked:checked});
            count.textContent=`${checked.length} de ${boxes.length} completados`;
            buttons.forEach(b=>b.disabled=boxes.length===0 || checked.length!==boxes.length);
        };
        const save = () => {
            clearTimeout(timer); const payload=answer.value;
            queue=queue.catch(()=>{}).then(async()=>{
                status.textContent='Guardando marcas…';
                try {
                    const body=new URLSearchParams({id:root.dataset.assignment,answer:payload,__RequestVerificationToken:form.querySelector('[name="__RequestVerificationToken"]').value});
                    const response=await fetch('/mi-trabajo?handler=ChecklistDraft',{method:'POST',body,credentials:'same-origin',headers:{Accept:'application/json'}});
                    if(!response.ok) throw new Error(response.status===409?'La tarea cambió. Recarga antes de continuar.':'No se pudieron guardar las marcas. Se enviarán al entregar.');
                    status.textContent='Marcas guardadas como borrador.';
                } catch(error) { status.textContent=error.message; }
            }); return queue;
        };
        boxes.forEach(b=>b.addEventListener('change',()=>{update(); clearTimeout(timer); timer=setTimeout(save,250);}));
        form.addEventListener('submit', async e=>{
            if(ready) {ready=false;return;}
            if(waiting) {e.preventDefault();e.stopImmediatePropagation();return;}
            const returning=e.submitter?.value==='DEVOLUCION';
            if(!returning && boxes.some(b=>!b.checked)) {e.preventDefault();e.stopImmediatePropagation();status.textContent='Completa todos los puntos antes de enviar.';return;}
            e.preventDefault();e.stopImmediatePropagation();clearTimeout(timer);
            const submitter=e.submitter;waiting=true;
            await queue;
            // The delivery carries the full answer even when a draft could not be saved.
            // Use a new browser task: requestSubmit during the current submission event is ignored.
            setTimeout(()=>{
                waiting=false;
                if(!returning && boxes.some(b=>!b.checked)) {update();return;}
                ready=true;form.requestSubmit(submitter);
            },0);
        },true);
        update();
    });
})();
