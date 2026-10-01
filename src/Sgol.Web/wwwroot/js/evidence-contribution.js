(() => {
    "use strict";
    const focus = document.querySelector("[data-evidence-error], [data-evidence-success]");
    focus?.focus();
    document.querySelectorAll("[data-evidence-submit]").forEach(form => form.addEventListener("submit", () => {
        form.setAttribute("aria-busy", "true");
        const button = form.querySelector("button[type=submit]");
        button.disabled = true;
    }));
    const form = document.querySelector("[data-evidence-upload]");
    if (!form) return;
    const find = name => form.querySelector(`[data-upload-${name}]`);
    const fileInput = form.querySelector("input[type=file]");
    const selector = document.getElementById("evidence-requirement");
    const start = find("start"), cancel = find("cancel"), recover = find("recover");
    const refresh = find("refresh"), stop = find("stop"), contribute = find("contribute"), another = find("new");
    const status = find("status"), error = find("error"), progress = find("progress");
    let file = null, uploadIntent = null, flow = null, request = null, timer = null;
    let busy = false, stopped = false, generation = 0, scanState = null, uploadStarted = false;
    let contributionAttempted = false, contributed = false;
    const say = text => { find("status-text").textContent = text; };
    const stopPolling = () => { clearTimeout(timer); timer = null; stopped = true; stop.hidden = true; };
    function occupied(value) {
        busy = value;
        form.setAttribute("aria-busy", String(value));
        form.querySelector(".subida").setAttribute("aria-busy", String(value));
        fileInput.disabled = value || uploadStarted;
        if (selector) selector.disabled = value || uploadStarted && !contributed;
        selector?.closest("form").querySelector("button[type=submit]")?.toggleAttribute("disabled", value || uploadStarted && !contributed);
        form.querySelector("select[name=documentSubtype]")?.toggleAttribute("disabled", value || uploadStarted);
        start.disabled = value || !file || uploadStarted;
        [recover, refresh, another].forEach(b => { b.disabled = value; });
        contribute.disabled = value || scanState !== "LIMPIO";
    }
    function fail(message, correlation) {
        stopPolling();
        form.querySelector(".subida").classList.add("subida--error");
        find("error-text").textContent = message + (correlation ? " · " + correlation : "");
        error.hidden = false;
        error.focus();
        scanState = null;
        recover.hidden = !uploadIntent;
        another.hidden = false;
    }
    async function post(handler, values) {
        const body = new URLSearchParams({ __RequestVerificationToken: form.querySelector("input[name=__RequestVerificationToken]").value, ...values });
        const url = new URL(form.dataset.reload, location.origin);
        url.searchParams.set("handler", handler);
        const response = await fetch(url.pathname + url.search, {
            method: "POST", body, credentials: "same-origin", cache: "no-store", referrerPolicy: "no-referrer"
        });
        if (response.status === 401) { uploadIntent = flow = file = null; location.assign("/acceso"); throw new Error("Tu sesión terminó"); }
        if (!response.headers.get("content-type")?.includes("application/json")) throw new Error("No se pudo confirmar el resultado.");
        const result = await response.json();
        if (!response.ok) {
            const failure = new Error(result.message || "No fue posible completar esta operación.");
            failure.correlation = result.correlationId;
            if (response.status === 403 || response.status === 404) { uploadIntent = flow = file = null; }
            throw failure;
        }
        return result;
    }
    function renderState(result) {
        const allowed = ["PENDIENTE_CARGA", "PENDIENTE_ESCANEO", "LIMPIO", "INFECTADO", "INVALIDO", "ERROR_ESCANEO"];
        if (!allowed.includes(result.status)) throw new Error("No fue posible completar esta operación.");
        scanState = result.status;
        form.querySelector(".subida").classList.toggle("subida--error", ["INFECTADO", "INVALIDO", "ERROR_ESCANEO"].includes(scanState));
        const style = scanState === "LIMPIO" ? "exito" : ["INFECTADO", "INVALIDO", "ERROR_ESCANEO"].includes(scanState) ? "peligro" : "info";
        status.className = "alerta alerta--" + style;
        const icon = scanState === "LIMPIO" ? "check" : scanState === "INFECTADO" ? "shield" : style === "peligro" ? "alert" : "clock";
        find("icon").replaceChildren(document.querySelector(`[data-scan-icon=${icon}]`).content.cloneNode(true));
        say(result.message);
        error.hidden = true;
        contribute.hidden = scanState !== "LIMPIO";
        contribute.disabled = scanState !== "LIMPIO" || busy;
        refresh.hidden = false;
        recover.hidden = scanState !== "PENDIENTE_CARGA";
        another.hidden = ["LIMPIO", "INFECTADO", "INVALIDO", "ERROR_ESCANEO"].includes(scanState) ? false : true;
        if (scanState !== "PENDIENTE_ESCANEO") stopPolling();
    }
    function schedule() {
        clearTimeout(timer);
        if (!stopped && !document.hidden && scanState === "PENDIENTE_ESCANEO") {
            stop.hidden = false;
            timer = setTimeout(() => update(false), 5000);
        }
    }
    async function update(manual) {
        if (busy || !flow) return;
        if (manual) stopped = false;
        occupied(true);
        try { renderState(await post("FileStatus", { intention: flow })); }
        catch (failure) { fail(failure.message, failure.correlation); }
        finally { occupied(false); schedule(); }
    }
    function transfer(authorization, selectedFile) {
        return new Promise((resolve, reject) => {
            const headers = authorization.headers;
            const names = ["Content-Type", "Content-Length", "If-None-Match", "x-amz-meta-sgol-sha256", "x-amz-meta-sgol-media-type", "x-amz-meta-sgol-size-bytes"];
            if (authorization.method !== "PUT" || !headers || Object.keys(headers).length !== names.length ||
                names.some(n => typeof headers[n] !== "string") || headers["Content-Length"] !== String(selectedFile.size) ||
                headers["If-None-Match"] !== "*" || Date.parse(authorization.expiresAt) <= Date.now()) {
                reject(new Error("No fue posible completar esta operación.")); return;
            }
            // The signed URL stays in this transport closure only. Never send cookies or SGOL CSRF to S3.
            const xhr = new XMLHttpRequest();
            request = xhr;
            xhr.open("PUT", authorization.url);
            xhr.withCredentials = false;
            xhr.timeout = 30000;
            names.filter(n => n !== "Content-Length").forEach(n => xhr.setRequestHeader(n, headers[n]));
            xhr.upload.onprogress = event => {
                if (event.lengthComputable) {
                    const percent = Math.floor(event.loaded * 100 / event.total);
                    progress.value = percent;
                    say("Cargando archivo… " + percent + "%");
                }
            };
            xhr.onload = () => xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error("No se pudo confirmar el resultado."));
            xhr.onerror = xhr.ontimeout = xhr.onabort = () => reject(new Error("No se pudo confirmar el resultado."));
            xhr.send(selectedFile);
        });
    }
    function select(selected) {
        if (busy || uploadStarted) return;
        file = selected;
        uploadIntent = flow = null;
        error.hidden = true;
        find("name").textContent = file?.name || "";
        form.querySelector(".subida").classList.remove("subida--error");
        if (!file) { start.disabled = true; return; }
        if (file.size === 0 || file.size > 15728640) {
            file = null; fail(selected.size > 15728640 ? "El archivo supera 15 MiB." : "Revisa los datos del requisito seleccionado.");
        } else {
            const extension = file.name.split(".").at(-1)?.toLowerCase();
            const valid = form.dataset.kind === "FOTOGRAFIA" ?
                (file.type === "image/jpeg" && ["jpg", "jpeg"].includes(extension)) || (file.type === "image/png" && extension === "png") :
                file.type === "application/pdf" && extension === "pdf";
            if (!valid) { file = null; fail("El tipo de archivo no está permitido para este requisito."); }
        }
        start.disabled = !file;
    }
    fileInput.addEventListener("change", () => select(fileInput.files[0] || null));
    const zone = form.querySelector(".subida__zona");
    zone.addEventListener("dragover", event => { event.preventDefault(); });
    zone.addEventListener("drop", event => { event.preventDefault(); if (event.dataTransfer.files.length === 1) select(event.dataTransfer.files[0]); });
    form.addEventListener("submit", async event => {
        event.preventDefault();
        if (busy || !file || uploadStarted) return;
        const current = ++generation;
        uploadStarted = true; occupied(true); error.hidden = true; cancel.hidden = false;
        stopPolling(); say("Preparando el archivo…");
        try {
            const bytes = await file.arrayBuffer();
            const digest = await crypto.subtle.digest("SHA-256", bytes);
            if (current !== generation) return;
            const sha256 = [...new Uint8Array(digest)].map(b => b.toString(16).padStart(2, "0")).join("");
            const prepared = await post("PrepareUpload", {
                requirementCode: form.querySelector("input[name=requirementCode]").value,
                originalFileName: file.name, declaredMediaType: file.type, sizeBytes: String(file.size), sha256,
                documentSubtype: form.querySelector("select[name=documentSubtype]")?.value || ""
            });
            if (current !== generation) return;
            uploadIntent = prepared.intention;
            let issued = await post("Upload", { intention: uploadIntent });
            if (current !== generation) return;
            flow = issued.intention;
            progress.hidden = false; say("Cargando archivo…");
            await transfer(issued.upload, file);
            issued = null; request = null;
            if (current !== generation) return;
            say("Confirmando carga…");
            const completed = await post("CompleteUpload", { intention: flow });
            if (current !== generation) return;
            renderState(completed); stopped = false;
        } catch (failure) { if (current === generation) fail(failure.message, failure.correlation); }
        finally { if (current === generation) { cancel.hidden = true; occupied(false); schedule(); } }
    });
    cancel.addEventListener("click", () => {
        generation++; request?.abort(); request = null; stopPolling(); cancel.hidden = true;
        say("Se detuvo la carga en este navegador. No se aportó evidencia.");
        recover.hidden = !uploadIntent; another.hidden = false; occupied(false); another.focus();
    });
    recover.addEventListener("click", async () => {
        if (busy || !uploadIntent) return;
        occupied(true); error.hidden = true;
        try {
            if (contributionAttempted && flow) { finish(await post("ContributeFile", { intention: flow })); return; }
            if (!flow) { const issued = await post("Upload", { intention: uploadIntent }); flow = issued.intention; }
            // Never retransmit an uncertain PUT. Complete verifies its existing object with the original key.
            renderState(await post("CompleteUpload", { intention: flow })); stopped = false;
        } catch (failure) { fail(failure.message, failure.correlation); }
        finally { occupied(false); schedule(); }
    });
    refresh.addEventListener("click", () => update(true));
    stop.addEventListener("click", () => { stopPolling(); say("Se detuvo el seguimiento. El análisis puede continuar."); refresh.focus(); });
    contribute.addEventListener("click", async () => {
        if (busy || !flow || scanState !== "LIMPIO") return;
        occupied(true); stopPolling(); say("Aportando evidencia…");
        contributionAttempted = true;
        try {
            finish(await post("ContributeFile", { intention: flow }));
        } catch (failure) { fail(failure.message, failure.correlation); }
        finally { occupied(false); }
    });
    function finish(result) {
        contributed = true;
        form.querySelector(".subida").classList.remove("subida--error");
        error.hidden = true;
        status.className = "alerta alerta--exito";
        find("icon").replaceChildren(document.querySelector("[data-scan-icon=check]").content.cloneNode(true));
        document.querySelector("[data-evidence-empty]").hidden = true;
        const requirement = form.querySelector("input[name=requirementCode]").value;
        document.querySelector(`[data-evidence-requirement="${requirement}"] [data-evidence-recorded]`).textContent = " — Se aportó la evidencia.";
        say(result.message); status.tabIndex = -1; status.focus();
        uploadIntent = flow = file = null; scanState = null;
        [contribute, refresh, recover, another].forEach(b => { b.hidden = true; });
        if (selector) {
            selector.querySelector(`option[value="${form.querySelector("input[name=requirementCode]").value}"]`)?.remove();
            selector.value = ""; selector.disabled = false;
            selector.closest("form").querySelector("button[type=submit]").disabled = false;
        }
        // Keep this completed upload immutable while allowing a different captured requirement.
    }
    another.addEventListener("click", () => { stopPolling(); location.assign(form.dataset.reload + "#aportar-evidencia"); });
    document.addEventListener("visibilitychange", () => { if (document.hidden) clearTimeout(timer); else schedule(); });
    window.addEventListener("pagehide", () => { generation++; stopPolling(); request?.abort(); uploadIntent = flow = file = null; });
})();
