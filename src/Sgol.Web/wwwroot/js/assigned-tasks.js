(() => {
    "use strict";
    const dialog = document.querySelector('[data-assigned-task-detail]');
    if (!dialog) return;
    dialog.showModal();
    const form = dialog.querySelector('[data-assigned-task-submit]');
    const close = dialog.querySelector('[data-dialog-close]');
    const cameraRoot = form?.querySelector('[data-camera-capture]');
    let camera = null, capture = null, uploading = false, request = null, submitting = false, preparedSubmission = false;
    const upload = form?.querySelector('[data-camera-upload]');
    const submit = form?.querySelector('button[type=submit]');
    const conclusion = form?.querySelector('[name=conclusion]');
    let selectedPhotos = [];
    const screenshots = form?.querySelector('[data-screenshots]');
    if (screenshots) {
        screenshots.addEventListener('change', () => {
            const files = Array.from(screenshots.files);
            const bytes = files.reduce((total, file) => total + file.size, 0);
            const minimum = Number(screenshots.dataset.min), maximum = Number(screenshots.dataset.max);
            const message = files.length < minimum || files.length > maximum
                ? `Selecciona de ${minimum} a ${maximum} capturas de pantalla.`
                : files.some(file => file.size < 1 || file.size > 15728640 || !['image/jpeg', 'image/png'].includes(file.type))
                    ? 'Cada captura debe ser JPG/PNG, de hasta 15 MiB.'
                    : bytes > Number(screenshots.dataset.maxBytes) ? 'Las capturas superan el tamaño total permitido.' : '';
            screenshots.setCustomValidity(message);
            form.querySelector('[data-screenshots-status]').textContent = message || `${files.length} capturas seleccionadas · ${(bytes / 1048576).toFixed(2)} MiB`;
        });
    }
    function updateControls() {
        if (!cameraRoot) return;
        form.querySelector('[name=captureSessionId]').value = capture?.captureSessionId || '';
        form.querySelector('[name=photographIds]').value = JSON.stringify(selectedPhotos.map(p => p.id));
        const ready = selectedPhotos.length >= Number(cameraRoot.dataset.cameraMin) && selectedPhotos.length <= Number(cameraRoot.dataset.cameraMax)
            && capture && form.checkValidity();
        const occupied = uploading || submitting || cameraRoot.getAttribute('aria-busy') === 'true';
        upload.disabled = occupied || !selectedPhotos.length || selectedPhotos.every(p => p.clean);
        submit.disabled = occupied || !ready;
    }
    async function post(handler, values, signal) {
        const data = new FormData();
        data.append('__RequestVerificationToken', form.querySelector('[name=__RequestVerificationToken]').value);
        data.append('id', dialog.dataset.assignedTaskDetail);
        Object.entries(values).forEach(([key, value]) => data.append(key, value));
        const response = await fetch(`/mi-trabajo?handler=${handler}`, { method: 'POST', body: data, credentials: 'same-origin', cache: 'no-store', signal });
        if (!/application\/(?:problem\+)?json/i.test(response.headers.get('content-type') || '')) throw new Error('No se confirmó el resultado. Recupera la carga o inicia sesión nuevamente.');
        const result = await response.json();
        if (!response.ok) { const failure = new Error(result.message || result.title || 'No se pudo completar la carga.'); failure.status = response.status; throw failure; }
        return result;
    }
    if (cameraRoot) {
        camera = window.SGOLCamera(cameraRoot, {
            onopen: async () => {
                if (!capture) capture = await post('BeginCamera', {});
                if (Date.parse(capture.expiresAt) <= Date.now()) throw new Error('La sesión de captura venció. Recarga la tarea y vuelve a tomar las fotografías.');
                updateControls();
            },
            onchange: photos => {
                selectedPhotos = photos;
                updateControls();
            },
            onremove: async photo => { if (photo.clean || photo.uncertain) await post('DeleteCameraPhoto', { captureSessionId: capture.captureSessionId, photographId: photo.id }); }
        });
        conclusion.addEventListener('input', updateControls);
        form.addEventListener('change', updateControls);
        async function uploadPhotographs() {
            if (uploading || !capture || !camera.valid()) { camera.error('Toma la cantidad de fotografías requerida antes de enviar.'); return false; }
            if (camera.photos.every(p => p.clean)) return true;
            uploading = true; request = new AbortController(); camera.error(''); camera.setBusy(true); close.disabled = true;
            form.querySelector('[data-camera-cancel-upload]').hidden = false;
            try {
                for (const photo of camera.photos) {
                    if (photo.clean) continue;
                    photo.status = 'Verificando carga…'; camera.render();
                    let result;
                    if (photo.uncertain) {
                        try { result = await post('CameraStatus', { captureSessionId: capture.captureSessionId, photographId: photo.id }, request.signal); }
                        catch (failure) { if (failure.status !== 404) throw failure; }
                    }
                    if (!result) {
                        photo.uncertain = true; photo.status = 'Subiendo y analizando…'; camera.render();
                        result = await post('UploadCamera', { captureSessionId: capture.captureSessionId, photographId: photo.id, photo: photo.file }, request.signal);
                    }
                    if (result.photographId !== photo.id || result.status !== 'LIMPIO') throw new Error('No se pudo verificar la fotografía.');
                    photo.clean = true; photo.uncertain = false; photo.status = 'Lista'; camera.render();
                }
                cameraRoot.querySelector('[data-camera-status]').textContent = 'Fotografías verificadas. Pulsa Enviar a revisión para confirmar la entrega.';
                return true;
            } catch (failure) {
                const current = camera.photos.find(p => !p.clean); if (current) current.status = 'Pendiente · reintenta o elimina la toma';
                camera.error(failure.name === 'AbortError' ? 'Carga detenida. Pulsa Subir fotografías para recuperar el resultado.' : failure.message);
                return false;
            } finally {
                uploading = false; request = null; close.disabled = false; camera.setBusy(false);
                form.querySelector('[data-camera-cancel-upload]').hidden = true;
                upload.textContent = camera.photos.some(p => p.uncertain) ? 'Recuperar o reintentar carga' : 'Subir fotografías';
            }
        }
        upload.addEventListener('click', () => { if (!submitting) uploadPhotographs(); });
        form.addEventListener('submit', async event => {
            if (event.submitter?.value === 'DEVOLUCION') return;
            if (preparedSubmission) return;
            event.preventDefault();
            const chosenSubmit = event.submitter || submit;
            if (uploading || submitting) return;
            if (!camera.valid() || !form.reportValidity()) { camera.error('Toma las fotografías requeridas y revisa los campos antes de enviar.'); return; }
            submitting = true; updateControls();
            const verified = await uploadPhotographs();
            // Already verified photos resolve immediately. Leave the original cancelled
            // submit event before requestSubmit, or the browser ignores the nested send.
            await new Promise(resolve => setTimeout(resolve, 0));
            submitting = false; updateControls();
            if (!verified || !form.reportValidity()) return;
            preparedSubmission = true;
            form.requestSubmit(chosenSubmit);
        });
        form.querySelector('[data-camera-cancel-upload]').addEventListener('click', () => request?.abort());
    }
    dialog.addEventListener('close', () => {
        request?.abort(); camera?.dispose();
        const url = new URL(location.href);
        url.searchParams.delete('assignedTask');
        url.searchParams.delete('handler');
        history.replaceState(null, '', url);
        document.querySelector(`[data-assigned-task="${dialog.dataset.assignedTaskDetail}"]`)?.focus();
    });
    dialog.addEventListener('cancel', event => {
        if (uploading || form?.getAttribute('aria-busy') === 'true') event.preventDefault();
    });
    form?.addEventListener('submit', event => {
        if (event.defaultPrevented) return;
        const returning = event.submitter?.value === 'DEVOLUCION';
        if (camera && !preparedSubmission && !returning) return;
        if (uploading || !returning && camera && (!camera.valid() || camera.photos.some(p => !p.clean))) {
            event.preventDefault(); camera?.error('Toma y sube todas las fotografías antes de enviar la tarea.'); return;
        }
        form.setAttribute('aria-busy', 'true');
        if (event.submitter?.name) {
            const choice = document.createElement('input');
            choice.type = 'hidden'; choice.name = event.submitter.name; choice.value = event.submitter.value;
            form.appendChild(choice);
            event.submitter.disabled = true;
        }
        form.querySelector('button[type="submit"]').disabled = true;
        close.disabled = true;
        form.querySelector('[data-assigned-task-busy]').hidden = false;
    });
})();
