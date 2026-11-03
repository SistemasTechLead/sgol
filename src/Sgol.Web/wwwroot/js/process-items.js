(() => {
    document.querySelectorAll('[data-item-quantity]').forEach(input => {
        if (input.dataset.itemReady) return;
        input.dataset.itemReady = 'true';
        const formula = input.form.querySelector('[data-item-formula]');
        const update = () => {
            const quantity = Number(input.value), minutes = Number(input.dataset.itemMinutes);
            formula.textContent = input.value !== '' && Number.isSafeInteger(quantity) && quantity >= 0
                ? quantity === 0 ? 'Cero: omitir tareas intermedias y pasar a auditoría final, conservando su carga y entregable.'
                    : `${quantity} × ${minutes} min = ${(BigInt(quantity) * BigInt(minutes)).toString()} min de carga siguiente.`
                : 'Indica una cantidad entera. Vacío no significa cero.';
        };
        input.addEventListener('input', update); update();
    });
    document.querySelector('[data-item-conflict]')?.focus();
    // Keep native file inputs alive across validation responses. Files stay only in
    // this page; every retry still passes through the normal server validators.
    document.querySelectorAll('[data-process-delivery]').forEach(form => {
        if (!form.querySelector('input[type=file][name=Document], input[type=file][name=screenshots]')) return;
        form.dataset.processSubmitReady = 'true';
    });
    if (document.documentElement.dataset.processSubmitListening) return;
    document.documentElement.dataset.processSubmitListening = 'true';
    document.addEventListener('submit', async event => {
        const form = event.target;
        if (form.dataset.processSubmitReady !== 'true' || event.defaultPrevented) return;
        event.preventDefault();
        if (form.getAttribute('aria-busy') === 'true') return;
        const status = form.querySelector('[data-process-submit-status]');
        const payload = new FormData(form);
        if (event.submitter?.name) payload.set(event.submitter.name, event.submitter.value);
        const buttons = Array.from(form.querySelectorAll('button[type=submit]'));
        const disabled = buttons.map(button => button.disabled);
        const busy = form.querySelector('[data-assigned-task-busy]');
        form.setAttribute('aria-busy', 'true');
        buttons.forEach(button => button.disabled = true);
        if (busy) busy.hidden = false;
        status.hidden = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: payload, credentials: 'same-origin', cache: 'no-store' });
            if (response.redirected) { window.location.assign(response.url); return; }
            if (![400, 409, 422].includes(response.status)) throw new Error('No se pudo confirmar la entrega. Conservamos el archivo y los datos; puedes reintentar.');
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            const returned = page.querySelector('[data-process-delivery]');
            if (!returned || returned.elements.AttemptId?.value !== form.elements.AttemptId.value)
                throw new Error('El estado de la etapa cambió o la sesión necesita recargarse. El archivo sigue seleccionado en esta página.');
            const resolution = form.querySelector('[data-process-resolution]');
            const nextResolution = returned.querySelector('[data-process-resolution]');
            if (resolution && nextResolution) resolution.replaceChildren(...Array.from(nextResolution.childNodes));
            const csrf = returned.elements.__RequestVerificationToken;
            if (csrf) form.elements.__RequestVerificationToken.value = csrf.value;
            const serverError = page.querySelector('[data-process-error]')?.textContent.trim();
            status.textContent = (serverError || 'Revisa los datos y las opciones de la entrega.') + ' El archivo sigue seleccionado; no necesitas adjuntarlo otra vez.';
            status.hidden = false;
            (form.querySelector('[data-item-conflict]') || status).focus();
        } catch (error) {
            status.textContent = error instanceof TypeError ? 'No se pudo conectar para confirmar la entrega. El archivo sigue seleccionado; reintenta.'
                : error.message || 'No se pudo confirmar la entrega. El archivo sigue seleccionado; reintenta.';
            status.hidden = false; status.focus();
        } finally {
            form.removeAttribute('aria-busy');
            buttons.forEach((button, index) => button.disabled = disabled[index]);
            if (busy) busy.hidden = true;
        }
    });
})();
