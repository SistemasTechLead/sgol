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
        let queue=Promise.resolve(), timer, ready=false, waiting=false, blocked=false, pending=null, navigating=false;
        let basis=root.dataset.draftBase;
        status.tabIndex=-1;
        const storageKey='sgol-checklist:'+root.dataset.assignment;
        const actions=document.createElement('div'); actions.className='acciones'; root.append(actions);
        const compare=document.createElement('div'); root.append(compare);
        const action=(label,run)=>{const b=document.createElement('button');b.type='button';b.className='boton boton--secundario';b.textContent=label;b.addEventListener('click',()=>{run();status.focus();});actions.append(b);return b;};
        const remember=()=>{try {sessionStorage.setItem(storageKey,JSON.stringify({basis,answer:answer.value,pending,labels:boxes.filter(b=>b.checked).map(b=>b.closest('label').textContent.trim())}));return true;} catch {return false;} };
        const reload=()=>{if(remember()) {navigating=true;location.reload();}else {status.textContent='El navegador no permite conservar una copia al recargar. Copia tus marcas locales antes de salir.';compare.textContent=boxes.filter(b=>b.checked).map(b=>b.closest('label').textContent.trim()).join(' · ') || 'Sin marcas locales.';}};
        const forget=()=>{try {sessionStorage.removeItem(storageKey);} catch {} };
        const update = () => {
            const checked=boxes.filter(b=>b.checked).map(b=>b.dataset.checkId);
            answer.value=JSON.stringify({Definition:root.dataset.definition, Checked:checked});
            count.textContent=`${checked.length} de ${boxes.length} completados`;
            buttons.forEach(b=>b.disabled=blocked || boxes.length===0 || checked.length!==boxes.length);
        };
        const save = () => {
            clearTimeout(timer); timer=null; const payload=answer.value; remember();
            queue=queue.catch(()=>{}).then(async()=>{
                if(blocked) return;
                // Retain the exact request across network errors, including edits queued behind it.
                const request=pending || {payload,basis,id:crypto.randomUUID()}; pending=request; remember();
                status.textContent='Guardando marcas…';
                try {
                    const body=new URLSearchParams({id:root.dataset.assignment,answer:request.payload,draftBase:request.basis,saveId:request.id,__RequestVerificationToken:form.querySelector('[name="__RequestVerificationToken"]').value});
                    const response=await fetch('/mi-trabajo?handler=ChecklistDraft',{method:'POST',body,credentials:'same-origin',headers:{Accept:'application/json'}});
                    if(response.status>=500) throw new Error('Unconfirmed server response');
                    const result=await response.json();
                    if(!response.ok) {
                        blocked=true; pending=null; actions.replaceChildren(); compare.replaceChildren();
                        status.textContent=result.error || 'La tarea cambió. Tus marcas locales se conservaron. Recarga para revisar su estado.';
                        if(result.code==='TASK_CHECKLIST_DRAFT_STALE') {
                            const title=document.createElement('p');title.textContent='Marcas guardadas en el servidor para esta tarea:';compare.append(title);
                            const list=document.createElement('ul');compare.append(list);
                            boxes.forEach(box=>{const li=document.createElement('li');li.textContent=(result.checked.includes(box.dataset.checkId)?'Marcado: ':'Sin marcar: ')+box.closest('label').textContent.trim();list.append(li);});
                            action('Guardar mis marcas sobre esta versión',()=>{basis=result.draftBase;blocked=false;actions.replaceChildren();compare.replaceChildren();update();save();});
                        }
                        action('Recargar tarea (conservar copia local)',reload);
                        update(); return;
                    }
                    basis=result.draftBase; pending=null; actions.replaceChildren();
                    status.textContent='Marcas guardadas como borrador.';
                    if(answer.value===request.payload) forget(); else {remember();setTimeout(save,0);}
                } catch {
                    blocked=true;update();actions.replaceChildren();
                    status.textContent='No se confirmó el guardado. Tus marcas locales se conservaron; reintenta antes de entregar.';
                    remember();
                    action('Reintentar guardado',()=>{blocked=false;update();save();});
                    action('Recargar tarea (conservar copia local)',reload);
                }
            }); return queue;
        };
        boxes.forEach(b=>b.addEventListener('change',()=>{update(); remember(); clearTimeout(timer); timer=setTimeout(save,250);}));
        form.addEventListener('submit', async e=>{
            if(ready) {ready=false;return;}
            if(waiting) {e.preventDefault();e.stopImmediatePropagation();return;}
            const returning=e.submitter?.value==='DEVOLUCION';
            if(!returning && boxes.some(b=>!b.checked)) {e.preventDefault();e.stopImmediatePropagation();status.textContent='Completa todos los puntos antes de enviar.';return;}
            e.preventDefault();e.stopImmediatePropagation();clearTimeout(timer);
            const submitter=e.submitter;waiting=true;
            await queue;
            if(!returning) {await save(); if(blocked) {waiting=false;return;}}
            // Submit only after confirming the draft; a conflict keeps local marks editable.
            // Use a new browser task: requestSubmit during the current submission event is ignored.
            setTimeout(()=>{
                waiting=false;
                if(!returning && boxes.some(b=>!b.checked)) {update();return;}
                ready=true;form.requestSubmit(submitter);
            },0);
        },true);
        update();
        try {
            const local=JSON.parse(sessionStorage.getItem(storageKey) || 'null');
            if(local && local.answer!==answer.value) {
                blocked=true; update(); status.textContent='Hay una copia local sin confirmar. Puedes recuperarla antes de guardar.';
                action('Recuperar mis marcas locales',()=>{
                    const restored=JSON.parse(local.answer);
                    if(restored.Definition!==root.dataset.definition) {status.textContent='La definición cambió. Conserva esta copia local y compara los puntos antes de continuar.';compare.textContent=(local.labels || []).join(" · ") || "No había puntos marcados en la copia local.";return;}
                    boxes.forEach(b=>b.checked=restored.Checked.includes(b.dataset.checkId)); basis=local.basis; pending=local.pending || null; blocked=false;actions.replaceChildren();update();save();
                });
                action('Usar versión guardada',()=>{forget();blocked=false;pending=null;actions.replaceChildren();compare.replaceChildren();status.textContent='Versión guardada cargada.';update();});
            }
        } catch {}
        window.addEventListener('beforeunload',e=>{if(!navigating && (blocked || pending || timer)) {e.preventDefault();e.returnValue='';}});
    });
})();
