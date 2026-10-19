(() => {
    "use strict";
    window.SGOLCamera = (root, options = {}) => {
        const find = name => root.querySelector(`[data-camera-${name}]`);
        const dialog = find("dialog"), video = find("video"), preview = find("preview");
        const open = find("open"), shutter = find("shutter"), retry = find("retry"), use = find("use"), swap = find("switch");
        const deviceSelect = find("device"), deviceSelector = find("selector");
        const maximum = Number(root.dataset.cameraMax), minimum = Number(root.dataset.cameraMin), maxBytes = Number(root.dataset.cameraBytes);
        let stream = null, draft = null, draftUrl = null, busy = false, facing = "environment", generation = 0;
        let cameras = [], selectedDevice = "", opening = false;
        const rear = device => /back|rear|environment|traser|posterior|arrière|rück/i.test(device.label);
        const front = device => /front|user|frontal|delanter|avant/i.test(device.label);
        const photos = [];
        const error = message => { find("error").textContent = message; find("error").hidden = !message; };
        const say = text => { find("dialog-status").textContent = text; };
        function stop() { generation++; opening = false; stream?.getTracks().forEach(t => t.stop()); stream = null; video.srcObject = null; shutter.disabled = true; }
        function clearDraft() { if (draftUrl) URL.revokeObjectURL(draftUrl); draftUrl = null; draft = null; preview.removeAttribute("src"); }
        function render() {
            find("gallery").replaceChildren(...photos.map((photo, i) => {
                const card = document.createElement("figure"), img = document.createElement("img"), caption = document.createElement("figcaption"), remove = document.createElement("button");
                img.src = photo.url; img.alt = `Fotografía ${i + 1} seleccionada`;
                caption.textContent = `Foto ${i + 1} · ${(photo.file.size / 1048576).toFixed(2)} MiB · ${photo.status || "Por subir"}`;
                remove.type = "button"; remove.className = "boton boton--secundario"; remove.textContent = "Eliminar"; remove.disabled = busy;
                remove.setAttribute("aria-label", `Eliminar fotografía ${i + 1}`);
                remove.addEventListener("click", async () => {
                    if (busy) return;
                    setBusy(true);
                    try { await options.onremove?.(photo); photos.splice(photos.indexOf(photo), 1); URL.revokeObjectURL(photo.url); error(""); }
                    catch (failure) { error(failure.message); }
                    finally { setBusy(false); }
                });
                card.append(img, caption, remove); return card;
            }));
            open.disabled = busy || photos.length >= maximum;
            find("status").textContent = `${photos.length} de ${maximum} fotografías · ${(photos.reduce((n, p) => n + p.file.size, 0) / 1048576).toFixed(2)} MiB`;
            options.onchange?.(photos);
        }
        function setBusy(value) { busy = value; root.setAttribute("aria-busy", String(value)); render(); }
        async function activate(deviceId = selectedDevice) {
            stop(); const current = generation;
            opening = true; swap.disabled = true; if (deviceSelect) deviceSelect.disabled = true;
            video.hidden = false; preview.hidden = true; retry.hidden = use.hidden = true; swap.hidden = false;
            if (deviceSelector) deviceSelector.hidden = true;
            shutter.hidden = false; shutter.disabled = true;
            say("Abriendo cámara…");
            try {
                if (!window.isSecureContext) throw new Error("Abre SGOL con HTTPS para usar la cámara.");
                if (!navigator.mediaDevices?.getUserMedia) throw new Error("Este navegador no permite usar la cámara. Abre SGOL en un navegador compatible.");
                const request = selection => navigator.mediaDevices.getUserMedia({ audio: false, video: { ...selection, width: { ideal: 2560 }, height: { ideal: 1440 } } });
                let pending;
                try { pending = await request(deviceId ? { deviceId: { exact: deviceId } } : { facingMode: { exact: facing } }); }
                catch (failure) {
                    if (current !== generation || !dialog.open || document.hidden) return;
                    // Desktop webcams may not implement facingMode. Do not mask permission or hardware errors.
                    if (deviceId || !["OverconstrainedError", "NotFoundError"].includes(failure.name)) throw failure;
                    pending = await request({ facingMode: { ideal: facing } });
                }
                if (current !== generation || !dialog.open || document.hidden) { pending.getTracks().forEach(t => t.stop()); return; }
                const devices = await navigator.mediaDevices.enumerateDevices().catch(() => []);
                if (current !== generation || !dialog.open || document.hidden) { pending.getTracks().forEach(t => t.stop()); return; }
                cameras = devices.filter(d => d.kind === "videoinput" && d.deviceId && d.deviceId !== "default")
                    .filter((d, i, all) => all.findIndex(x => x.deviceId === d.deviceId) === i);
                let settings = (pending.getVideoTracks?.()[0] || pending.getTracks()[0])?.getSettings?.() || {};
                // When facingMode is ignored, labels exposed after permission can identify a rear camera.
                const preferred = !deviceId && settings.facingMode !== facing ? cameras.find(facing === "environment" ? rear : front) : null;
                if (preferred && preferred.deviceId !== settings.deviceId) {
                    pending.getTracks().forEach(t => t.stop());
                    pending = await request({ deviceId: { exact: preferred.deviceId } });
                    if (current !== generation || !dialog.open || document.hidden) { pending.getTracks().forEach(t => t.stop()); return; }
                    deviceId = preferred.deviceId;
                    settings = (pending.getVideoTracks?.()[0] || pending.getTracks()[0])?.getSettings?.() || {};
                }
                if (deviceId && settings.deviceId && settings.deviceId !== deviceId) {
                    pending.getTracks().forEach(t => t.stop());
                    throw new Error("El navegador no abrió la cámara seleccionada. Reabre la cámara e intenta seleccionar otra.");
                }
                selectedDevice = settings.deviceId || deviceId || "";
                if (["environment", "user"].includes(settings.facingMode)) facing = settings.facingMode;
                stream = pending; video.srcObject = stream;
                stream.getTracks().forEach(track => track.addEventListener?.("ended", () => {
                    if (!dialog.open || draft) return;
                    stop(); say("La cámara se desconectó. Pulsa Reabrir cámara para continuar.");
                    retry.hidden = false; retry.textContent = "Reabrir cámara";
                }, { once: true }));
                await video.play();
                if (current !== generation || !dialog.open) return;
                shutter.disabled = !(video.videoWidth > 0 && video.videoHeight > 0);
                if (deviceSelect) {
                    deviceSelect.replaceChildren(...cameras.map((device, i) => {
                        const option = document.createElement("option"); option.value = device.deviceId;
                        option.textContent = device.label || `Cámara ${i + 1}`; return option;
                    }));
                    deviceSelect.value = selectedDevice;
                }
                if (deviceSelector) deviceSelector.hidden = cameras.length < 2;
                swap.hidden = cameras.length < 2 && !navigator.mediaDevices.getSupportedConstraints?.().facingMode;
                const active = cameras.find(d => d.deviceId === selectedDevice);
                say((active?.label ? active.label + ". " : "") + "Encuadra la evidencia y pulsa Capturar."); shutter.focus();
            } catch (failure) {
                if (current !== generation || !dialog.open) return;
                stop();
                selectedDevice = "";
                const messages = { NotAllowedError: "Permite el acceso a la cámara en los ajustes del navegador y vuelve a intentar.", NotFoundError: "No se encontró una cámara en este dispositivo.", NotReadableError: "La cámara está ocupada o bloqueada. Cierra otras aplicaciones que la estén utilizando.", OverconstrainedError: "Esta cámara no admite la configuración solicitada." };
                say(messages[failure.name] || failure.message || "No se pudo abrir la cámara.");
                retry.hidden = false; retry.textContent = "Reabrir cámara"; swap.hidden = true;
            } finally {
                if (current === generation || !stream && !dialog.open) opening = false;
                if (!opening) { swap.disabled = false; if (deviceSelect) deviceSelect.disabled = false; }
            }
        }
        open.addEventListener("click", async () => {
            if (busy || photos.length >= maximum) return;
            error(""); clearDraft(); dialog.showModal();
            // Request browser permission directly from the user's click; authorize the capture session alongside it.
            const camera = activate();
            try { await options.onopen?.(); await camera; }
            catch (failure) { await camera; dialog.close(); error(failure.message); }
        });
        video.addEventListener("loadeddata", () => { if (dialog.open && stream) shutter.disabled = false; });
        shutter.addEventListener("click", async () => {
            if (!stream || shutter.disabled || !video.videoWidth || !video.videoHeight) return;
            shutter.disabled = true; swap.disabled = true; say("Preparando fotografía…");
            const current = generation;
            try {
                const scale = Math.min(1, 2560 / Math.max(video.videoWidth, video.videoHeight));
                const canvas = document.createElement("canvas"); canvas.width = Math.round(video.videoWidth * scale); canvas.height = Math.round(video.videoHeight * scale);
                canvas.getContext("2d", { alpha: false }).drawImage(video, 0, 0, canvas.width, canvas.height);
                const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/jpeg", 0.9));
                canvas.width = canvas.height = 0;
                if (current !== generation || !dialog.open) return;
                if (!blob || blob.type !== "image/jpeg" || blob.size < 1 || blob.size > 15728640) throw new Error("No se pudo preparar una foto válida de hasta 15 MiB. Vuelve a tomarla.");
                if (photos.reduce((n, p) => n + p.file.size, 0) + blob.size > maxBytes) throw new Error("Las fotografías superan el tamaño total permitido. Elimina una toma antes de continuar.");
                clearDraft(); draft = blob; draftUrl = URL.createObjectURL(blob); preview.src = draftUrl;
                stop(); video.hidden = true; preview.hidden = false; shutter.hidden = swap.hidden = true; if (deviceSelector) deviceSelector.hidden = true;
                retry.hidden = use.hidden = false; retry.textContent = "Reintentar"; say("Revisa la fotografía. Puedes usarla o volver a tomarla."); use.focus();
            } catch (failure) { if (current === generation) { say(failure.message); shutter.disabled = false; } }
            finally { swap.disabled = false; }
        });
        retry.addEventListener("click", () => { clearDraft(); activate(); });
        swap.addEventListener("click", () => {
            if (opening) return;
            const activeIndex = cameras.findIndex(d => d.deviceId === selectedDevice);
            const active = cameras[activeIndex];
            const opposite = active && rear(active) ? cameras.find(front) : active && front(active) ? cameras.find(rear) : null;
            const next = opposite || cameras.length > 1 && cameras[(activeIndex + 1) % cameras.length];
            facing = facing === "environment" ? "user" : "environment";
            activate(next?.deviceId || "");
        });
        deviceSelect?.addEventListener("change", () => { if (!opening && deviceSelect.value) activate(deviceSelect.value); });
        use.addEventListener("click", () => {
            if (!draft || photos.length >= maximum) return;
            const id = crypto.randomUUID();
            const file = new File([draft], `foto-${id}.jpg`, { type: "image/jpeg" });
            photos.push({ id, file, url: URL.createObjectURL(file), status: "Por subir", clean: false });
            dialog.close(); render();
        });
        find("close").addEventListener("click", () => dialog.close());
        dialog.addEventListener("close", () => { stop(); clearDraft(); open.focus(); });
        document.addEventListener("visibilitychange", () => { if (document.hidden) { stop(); if (dialog.open && !draft) { say("La cámara se apagó. Pulsa Reabrir cámara para continuar."); retry.hidden = false; retry.textContent = "Reabrir cámara"; } } });
        function dispose() { stop(); clearDraft(); for (const photo of photos) URL.revokeObjectURL(photo.url); photos.length = 0; }
        root.closest("[data-assigned-task-detail]")?.addEventListener("close", dispose);
        window.addEventListener("pagehide", dispose);
        render();
        return { photos, render, setBusy, error, dispose, valid: () => photos.length >= minimum && photos.length <= maximum };
    };
})();
