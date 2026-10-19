(() => {
    "use strict";
    const form = document.querySelector("[data-photo-evidence]"); if (!form) return;
    const find = name => form.querySelector(`[data-photo-${name}]`);
    let capture = null, busy = false, xhr = null, timer = null, commitAttempted = false, finished = false, controller = null;
    const say = message => { find("status").textContent = message; };
    async function post(handler, values) {
        const url = new URL(form.dataset.reload, location.origin); url.searchParams.set("handler", handler);
        const response = await fetch(url.pathname + url.search, { method: "POST", credentials: "same-origin", cache: "no-store", referrerPolicy: "no-referrer", signal: controller?.signal,
            body: new URLSearchParams({ __RequestVerificationToken: form.querySelector("[name=__RequestVerificationToken]").value, ...values }) });
        if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("No se confirmó el resultado. Recarga o inicia sesión nuevamente.");
        const result = await response.json();
        if (!response.ok) throw new Error(result.message || "No se pudo completar la operación.");
        return result;
    }
    const context = () => ({ requirementCode: form.querySelector("[name=requirementCode]").value,
        ...(form.dataset.replacement === "true" ? { replacement: form.querySelector("[name=replacement]").value, reason: form.querySelector("[name=reason]").value } : {}) });
    const camera = window.SGOLCamera(form.querySelector("[data-camera-capture]"), {
        onopen: async () => { if (!capture) capture = await post("BeginPhotoCamera", context()); },
        onchange: photos => {
            find("upload").disabled = busy || finished || commitAttempted || !photos.length || photos.every(p => p.flow);
            find("contribute").disabled = busy || finished || !photos.length || photos.some(p => !p.clean);
            find("refresh").disabled = busy || finished;
        }
    });
    function occupied(value) {
        busy = value; form.setAttribute("aria-busy", String(value)); camera.setBusy(value || finished || commitAttempted);
        form.querySelector("[name=reason]")?.toggleAttribute("readonly", value || commitAttempted || camera.photos.some(p => p.uploadIntent));
    }
    function transfer(authorization, photo) {
        return new Promise((resolve, reject) => {
            const headers = authorization.headers;
            const names = ["Content-Type", "Content-Length", "If-None-Match", "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type", "x-amz-meta-sgol-size-bytes"];
            if (authorization.method !== "PUT" || !headers || Object.keys(headers).length !== names.length ||
                names.some(n => typeof headers[n] !== "string") || headers["Content-Length"] !== String(photo.file.size) ||
                headers["If-None-Match"] !== "*" || Date.parse(authorization.expiresAt) <= Date.now()) return reject(new Error("La autorización de carga no es válida."));
            xhr = new XMLHttpRequest(); xhr.open("PUT", authorization.url); xhr.withCredentials = false; xhr.timeout = 30000;
            names.filter(n => n !== "Content-Length").forEach(n => xhr.setRequestHeader(n, headers[n]));
            xhr.upload.onprogress = e => { if (e.lengthComputable) { photo.status = `Subiendo ${Math.floor(e.loaded * 100 / e.total)}%`; camera.render(); } };
            xhr.onload = () => xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error("No se confirmó la carga. Actualiza su estado o vuelve a tomar la foto."));
            xhr.onerror = xhr.ontimeout = xhr.onabort = () => reject(new Error("No se confirmó la carga. Actualiza su estado o vuelve a tomar la foto."));
            xhr.send(photo.file);
        });
    }
    function state(photo, result) {
        if (!["PENDIENTE_CARGA", "PENDIENTE_ESCANEO", "LIMPIO", "INFECTADO", "INVALIDO", "ERROR_ESCANEO"].includes(result.status)) throw new Error("El estado de la fotografía no es válido.");
        photo.clean = result.status === "LIMPIO"; photo.scanState = result.status; photo.status = result.message; camera.render();
    }
    function schedule() {
        clearTimeout(timer);
        if (!finished && !commitAttempted && !document.hidden && camera.photos.some(p => p.scanState === "PENDIENTE_ESCANEO")) timer = setTimeout(update, 5000);
    }
    async function update() {
        if (busy || finished) return;
        occupied(true); camera.error("");
        try {
            for (const photo of camera.photos.filter(p => p.flow && !p.clean)) {
                const result = await post(photo.scanState === "PENDIENTE_CARGA" || !photo.scanState ? "CompleteUpload" : "FileStatus", { intention: photo.flow }); state(photo, result);
            }
        } catch (failure) { camera.error(failure.message); }
        finally { occupied(false); schedule(); }
    }
    find("upload").addEventListener("click", async () => {
        if (busy || !capture || !camera.valid() || !form.reportValidity()) return;
        controller = new AbortController(); occupied(true); clearTimeout(timer); camera.error(""); find("cancel").hidden = false;
        try {
            for (const photo of camera.photos) {
                if (photo.flow) continue;
                photo.status = "Preparando…"; camera.render();
                const hash = [...new Uint8Array(await crypto.subtle.digest("SHA-256", await photo.file.arrayBuffer()))].map(b => b.toString(16).padStart(2, "0")).join("");
                if (!photo.uploadIntent) photo.uploadIntent = (await post("PrepareUpload", { ...context(), originalFileName: photo.file.name,
                    declaredMediaType: photo.file.type, sizeBytes: String(photo.file.size), sha256: hash, captureToken: capture.token })).intention;
                const issued = await post("Upload", { intention: photo.uploadIntent }); photo.flow = issued.intention;
                await transfer(issued.upload, photo); xhr = null;
                state(photo, await post("CompleteUpload", { intention: photo.flow }));
            }
            say("La carga terminó. Espera a que todas las fotografías estén listas para aportar la evidencia.");
        } catch (failure) { camera.error(failure.message); }
        finally { controller = null; find("cancel").hidden = true; find("refresh").hidden = false; occupied(false); schedule(); }
    });
    find("refresh").addEventListener("click", update);
    find("cancel").addEventListener("click", () => { controller?.abort(); xhr?.abort(); clearTimeout(timer); });
    find("contribute").addEventListener("click", async () => {
        if (busy || !camera.valid() || camera.photos.some(p => !p.clean) || finished) return;
        if (form.dataset.replacement === "true" && !commitAttempted && !window.confirm("Se sustituirá el conjunto de fotografías. La versión anterior permanecerá en el historial. ¿Continuar?")) return;
        commitAttempted = true; occupied(true); clearTimeout(timer); camera.error("");
        try {
            const result = await post("ContributePhotographs", { intentions: JSON.stringify(camera.photos.map(p => p.flow)) });
            finished = true; say(result.message); find("contribute").hidden = true; find("refresh").hidden = true; find("upload").hidden = true;
            const link = document.createElement("a"); link.href = form.dataset.reload + "#versiones-evidencia"; link.className = "boton boton--secundario"; link.textContent = "Ver fotografías e historial"; find("status").after(link);
            camera.dispose();
        } catch (failure) { camera.error(failure.message + " Reintenta la aportación para recuperar el resultado."); }
        finally { occupied(false); }
    });
    form.addEventListener("submit", event => event.preventDefault());
    document.addEventListener("visibilitychange", () => { if (document.hidden) clearTimeout(timer); else schedule(); });
    window.addEventListener("pagehide", () => { clearTimeout(timer); xhr?.abort(); });
})();
