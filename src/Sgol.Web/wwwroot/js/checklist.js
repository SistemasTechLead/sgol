(() => {
    'use strict';
    document.querySelectorAll('[data-checklist-editor]').forEach(root => {
        const form = root.closest('form'), requirement = form.querySelector('[name="Input.Requirement"]');
        const hidden = root.querySelector('[data-checklist-definition]'), rows = root.querySelector('[data-checklist-rows]');
        const point = (isTitle = false) => ({Id: crypto.randomUUID(), Text: '', ...(isTitle ? {IsTitle:true} : {})});
        let groups;
        const partition = points => {
            const result = []; let group = {title:point(true), items:[]};
            points.forEach(p => {
                if (p.IsTitle) {
                    if (group.title.Text || group.items.length) { result.push(group); group={title:p,items:[]}; }
                    else group.title=p;
                } else group.items.push(p);
            });
            result.push(group); return result;
        };
        try { groups=partition(JSON.parse(hidden.value || '[]')); } catch { groups=partition([]); }
        const flat = () => groups.flatMap(g => [g.title,...g.items]).filter(p => p.Text.trim());
        const sync = () => { hidden.value=JSON.stringify(flat()); };
        const updateActions = () => {
            const list=flat(), positions=new Map(list.map((p,i)=>[p.Id,i]));
            rows.querySelectorAll('[data-checklist-move]').forEach(button => {
                const index=positions.get(button.dataset.pointId), offset=Number(button.dataset.checklistMove);
                button.disabled=index===undefined || index+offset<0 || index+offset>=list.length;
            });
        };
        const render = () => {
            groups=groups.filter(g=>g.title.Text.trim() || g.items.some(p=>p.Text.trim()));
            if (!groups.length) groups=partition([]);
            rows.replaceChildren();
            const addRow = (container, p, group, groupIndex, placeholder = false) => {
                const row=document.createElement('div');
                row.className='checklist-row '+(p.IsTitle ? 'checklist-row--title' : 'checklist-row--item');
                row.classList.toggle('checklist-row--empty',!p.Text.trim());
                if (!p.IsTitle) {
                    const circle=document.createElement('span'); circle.className='checklist-circle'; circle.setAttribute('aria-hidden','true'); row.append(circle);
                }
                const input=document.createElement('textarea'); input.className='campo__control'; input.rows=1; input.maxLength=1000; input.value=p.Text;
                input.setAttribute('aria-label',p.IsTitle ? `Título del apartado ${groupIndex+1} (opcional)` : placeholder ? `Agregar casilla al apartado ${groupIndex+1}` : `Casilla ${group.items.indexOf(p)+1} del apartado ${groupIndex+1}`);
                input.placeholder=p.IsTitle ? 'Agrega un Titulo' : 'Agrega una Casilla';
                row.append(input); container.append(row);
                const addActions = () => {
                    const actions=document.createElement('div'); actions.className='checklist-row-actions';
                    const name=p.IsTitle ? `título del apartado ${groupIndex+1}` : `casilla ${group.items.indexOf(p)+1} del apartado ${groupIndex+1}`;
                    [['↑','Subir',-1],['↓','Bajar',1],['Eliminar','Eliminar',0]].forEach(([label,verb,offset])=>{
                        const button=document.createElement('button'); button.type='button'; button.className='boton boton--secundario'; button.textContent=label;
                        button.setAttribute('aria-label',`${verb} ${name}`);
                        if (offset) { button.dataset.checklistMove=offset; button.dataset.pointId=p.Id; }
                        button.addEventListener('click',()=>{
                            if (!offset) {
                                if (p.IsTitle) p.Text=''; else group.items.splice(group.items.indexOf(p),1);
                            } else {
                                const list=flat(), index=list.indexOf(p);
                                if (index<0 || !list[index+offset]) return;
                                [list[index],list[index+offset]]=[list[index+offset],list[index]]; groups=partition(list);
                            }
                            sync(); render(); rows.querySelector('textarea')?.focus();
                        }); actions.append(button);
                    }); row.append(actions);
                };
                if (p.Text.trim()) addActions();
                input.addEventListener('input',()=>{
                    p.Text=input.value; rows.querySelectorAll('textarea').forEach(field=>field.setCustomValidity(''));
                    row.classList.toggle('checklist-row--empty',!p.Text.trim());
                    if (p.Text.trim() && placeholder) {
                        placeholder=false; group.items.push(p);
                        input.setAttribute('aria-label',`Casilla ${group.items.length} del apartado ${groupIndex+1}`);
                        addActions(); addRow(container,point(),group,groupIndex,true); ensureNext();
                    } else if (p.Text.trim() && !row.querySelector('.checklist-row-actions')) addActions();
                    sync(); updateActions();
                });
            };
            const addGroup = (group,index) => {
                const container=document.createElement('div'); container.className='checklist-section'; container.dataset.checklistSection=''; rows.append(container);
                addRow(container,group.title,group,index);
                group.items.forEach(p=>addRow(container,p,group,index));
                addRow(container,point(),group,index,true);
            };
            const ensureNext = () => {
                if (groups.at(-1).items.length) { const next={title:point(true),items:[]}; groups.push(next); addGroup(next,groups.length-1); }
            };
            groups.forEach(addGroup); ensureNext(); updateActions();
        };
        const visibility=()=>{root.hidden=requirement.value!=='CHECKLIST'; rows.querySelectorAll('textarea').forEach(field=>field.setCustomValidity(''));};
        requirement.addEventListener('change',visibility); visibility(); render();
        form.addEventListener('submit',e=>{
            sync();
            if (requirement.value==='CHECKLIST' && !flat().some(p=>!p.IsTitle)) {
                e.preventDefault(); const input=rows.querySelector('.checklist-row--item textarea');
                input.setCustomValidity('Agrega al menos una casilla; los títulos son opcionales.'); input.reportValidity();
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
