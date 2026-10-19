(() => {
    "use strict";
    const root = document.querySelector("[data-direction-planning]");
    if (!root) return;
    if (!document.querySelector("#direction-plan-confirm")) document.getElementById("direction-plan-error")?.focus();
    const form = root.querySelector("[data-direction-create]");
    if (!form) return;
    document.getElementById("direction-plan-confirm")?.addEventListener("close", () => form.querySelector('button[type="submit"]')?.focus());
    const search = form.querySelector("#direction-search");
    const options = [...form.querySelectorAll("[data-task-option]")];
    const catalog = new Map(options.map(option => [option.dataset.code, option]));
    const selected = form.querySelector("[data-direction-selected]");
    const selectedCodes = () => new Set([...selected.querySelectorAll("[data-selected-code]")].map(row => row.dataset.selectedCode));
    const selectionStatus = form.querySelector("[data-selection-status]");
    const capacityDialog = root.querySelector("#direction-capacity");
    const capacityButton = form.querySelector("[data-plan-capacity-open]");
    const capacityValues = form.querySelector("[data-plan-capacity-values]");
    const capacityFingerprint = form.querySelector("[data-plan-capacity-fingerprint]");
    const capacitySummary = form.querySelector("[data-plan-capacity-summary]");
    const capacityStatus = capacityDialog.querySelector("[data-plan-capacity-status]");
    const capacityWorkers = capacityDialog.querySelector("[data-plan-capacity-workers]");
    const capacityAccept = capacityDialog.querySelector("[data-plan-capacity-accept]");
    let capacityState = null, capacityRequest = 0, capacityDailyDraft = null;
    function describeCapacity() {
        try { const values = JSON.parse(capacityValues.value || "[]"); capacitySummary.textContent = values.length ? `${values.length} ${values.length === 1 ? "ajuste de capacidad preparado" : "ajustes de capacidad preparados"}. Se aplicarán al guardar el plan.` : ""; }
        catch { capacityValues.value = "[]"; capacityFingerprint.value = ""; }
    }
    function invalidateCapacity() {
        if (capacityFingerprint.value) capacitySummary.textContent = "Las tareas o el periodo cambiaron. Vuelve a revisar Asignar cargas de trabajo.";
        capacityValues.value = "[]"; capacityFingerprint.value = ""; capacityState = null; capacityDailyDraft = null; capacityRequest++;
    }
    const roleLabels = JSON.parse(root.dataset.roleLabels);
    const roleLabel = role => roleLabels[role] || role;
    capacityButton.addEventListener("click", async () => {
        capacityDialog.showModal(); capacityAccept.disabled = true; capacityWorkers.replaceChildren();
        capacityStatus.textContent = "Consultando candidatos y minutos pendientes…";
        capacityButton.disabled = true;
        const request = ++capacityRequest;
        try {
            const response = await fetch("/planificacion?handler=DirectionCapacity", { method: "POST", body: new FormData(form), credentials: "same-origin", cache: "no-store" });
            if (response.status === 401) { window.location.assign("/acceso"); return; }
            if (response.status === 403) throw new Error("No tienes permiso para consultar estas cargas.");
            if (!response.ok) throw new Error("No se pudo consultar la carga. Revisa las tareas y el periodo e inténtalo de nuevo.");
            const state = await response.json();
            if (request !== capacityRequest || !capacityDialog.open) return;
            capacityState = state;
            const saved = capacityFingerprint.value === state.fingerprint ? JSON.parse(capacityValues.value || "[]") : [];
            capacityStatus.textContent = state.workers.length ? `Vigencia: ${state.from} — ${state.through}. ${state.workers.length} personas con rol para las tareas del plan. La capacidad considera sus días disponibles.` : "No hay candidatos para estas tareas y este periodo. Agrega tareas o revisa los roles de los empleados.";
            if (state.specialistGaps?.length) capacityStatus.textContent += " Especializaciones sin personal elegible: " + state.specialistGaps.map(gap => `${gap.specialization} (${gap.tasks.length} tareas)`).join("; ") + ". Vincula personal capacitado en Personas y accesos.";
            const days = [];
            const through = new Date(`${state.through}T00:00:00Z`);
            for (let day = new Date(`${state.from}T00:00:00Z`); day <= through; day.setUTCDate(day.getUTCDate() + 1)) {
                days.push(day.toISOString().slice(0, 10));
            }
            const dayLabel = new Intl.DateTimeFormat("es-MX", { weekday: "short", day: "2-digit", month: "short", timeZone: "UTC" });
            const container = document.createElement("div"); container.className = "tabla-contenedor direction-capacity__scroll";
            container.tabIndex = 0; container.setAttribute("role", "region"); container.setAttribute("aria-label", "Incidencias por empleado y día; desplaza la tabla para ver todas las fechas");
            const table = document.createElement("table"); table.className = "tabla direction-capacity__table";
            table.setAttribute("aria-describedby", "direction-capacity-help");
            const caption = document.createElement("caption"); caption.textContent = "Capacidad por día (%) y porcentaje total del periodo";
            const head = document.createElement("thead"); head.className = "tabla__encabezado";
            const headings = document.createElement("tr");
            ["Empleado", ...days.map(day => dayLabel.format(new Date(`${day}T00:00:00Z`))), "Total (%)"].forEach(label => {
                const cell = document.createElement("th"); cell.scope = "col"; cell.textContent = label; headings.append(cell);
            });
            head.append(headings);
            const body = document.createElement("tbody");
            const dailyDraft = capacityDailyDraft?.fingerprint === state.fingerprint ? capacityDailyDraft.workers : {};
            state.workers.forEach(worker => {
                const row = document.createElement("tr"); row.className = "tabla__fila"; row.dataset.capacityPerson = worker.personId;
                const employee = document.createElement("th"); employee.scope = "row";
                const name = document.createElement("strong"); name.textContent = worker.name;
                const role = document.createElement("small"); role.textContent = roleLabel(worker.role);
                const current = document.createElement("small"); current.textContent = `${worker.currentLoadMinutes} min pendientes · ${worker.periodLoadMinutes} min en la vigencia · ${worker.availableMinutes.toLocaleString("es-MX")} min disponibles`;
                employee.append(name, role, current);
                if (worker.percent < 100) {
                    const limit = document.createElement("small"); limit.textContent = `Capacidad existente: máximo ${worker.percent}%`; employee.append(limit);
                }
                row.append(employee);
                const totalCell = document.createElement("td");
                const total = document.createElement("output"); total.dataset.capacityTotal = ""; total.setAttribute("aria-label", `Porcentaje total de ${worker.name}`);
                totalCell.append(total);
                const inputs = [];
                const update = () => {
                    const valid = inputs.every(input => input.value !== "" && input.checkValidity());
                    const percent = valid && inputs.length ? Math.round(inputs.reduce((sum, input) => sum + Number(input.value), 0) / inputs.length) : null;
                    total.value = percent === null ? "—" : `${percent}%`;
                    total.dataset.percent = percent === null ? "" : String(percent);
                    capacityAccept.disabled = !state.workers.length || [...capacityWorkers.querySelectorAll("[data-capacity-day]")].some(input => input.value === "" || !input.checkValidity());
                };
                days.forEach(day => {
                    const cell = document.createElement("td");
                    const input = document.createElement("input"); input.className = "campo__control"; input.type = "number";
                    input.min = 0; input.max = worker.percent; input.step = 1; input.required = true; input.dataset.capacityDay = day;
                    input.value = dailyDraft[worker.personId]?.[day] ?? saved.find(a => a.personId === worker.personId)?.percent ?? worker.percent;
                    input.setAttribute("aria-label", `Capacidad de ${worker.name}, ${day}, porcentaje entre 0 y ${worker.percent}`);
                    input.addEventListener("input", update); inputs.push(input); cell.append(input); row.append(cell);
                });
                row.append(totalCell); body.append(row); update();
            });
            table.append(caption, head, body); container.append(table); capacityWorkers.append(container);
            capacityAccept.disabled = state.workers.length === 0;
        } catch (error) { if (request === capacityRequest) capacityStatus.textContent = error.message; }
        finally { capacityButton.disabled = false; }
    });
    capacityDialog.querySelector("[data-plan-capacity-cancel]").addEventListener("click", () => capacityDialog.close());
    capacityDialog.addEventListener("close", () => { capacityRequest++; capacityButton.focus(); });
    capacityAccept.addEventListener("click", () => {
        if (!capacityState) return;
        const invalid = [...capacityWorkers.querySelectorAll("[data-capacity-day]")].find(input => input.value === "" || !input.checkValidity());
        if (invalid) { invalid.reportValidity(); invalid.focus(); return; }
        const rows = [...capacityWorkers.querySelectorAll("[data-capacity-person]")];
        capacityDailyDraft = { fingerprint: capacityState.fingerprint, workers: Object.fromEntries(rows.map(row => [row.dataset.capacityPerson,
            Object.fromEntries([...row.querySelectorAll("[data-capacity-day]")].map(input => [input.dataset.capacityDay, Number(input.value)]))])) };
        const values = rows.map(row => ({ personId: row.dataset.capacityPerson, percent: Number(row.querySelector("[data-capacity-total]").dataset.percent) }))
            .filter(a => a.percent !== capacityState.workers.find(w => w.personId === a.personId).percent);
        capacityValues.value = JSON.stringify(values); capacityFingerprint.value = values.length ? capacityState.fingerprint : "";
        describeCapacity(); capacityDialog.close();
    });
    describeCapacity();
    const normalize = value => value.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase("es-MX");
    function filter() {
        const query = normalize(search.value.trim());
        let count = 0;
        options.forEach(option => { option.hidden = !query || !normalize(option.dataset.code + " " + option.dataset.name).includes(query); if (!option.hidden) count++; });
        form.querySelector("#direction-task-results").hidden = !query || count === 0;
        form.querySelector("[data-search-empty]").hidden = !query || count > 0;
    }
    function updateSelection() {
        const codes = selectedCodes();
        form.querySelector("[data-selection-empty]").hidden = codes.size > 0;
        options.forEach(option => {
            option.querySelector("button").disabled = form.dataset.operations === "true" || codes.has(option.dataset.code);
            option.querySelector("[data-added-label]").textContent = codes.has(option.dataset.code) ? "Agregada" : "";
        });
    }
    function add(code) {
        if (selectedCodes().has(code)) return false;
        const option = catalog.get(code); if (!option) throw new Error("Invalid catalog reference");
        const row = document.createElement("tr"); row.className = "tabla__fila"; row.dataset.selectedCode = code;
        const codeCell = document.createElement("td"); codeCell.textContent = code;
        const input = document.createElement("input"); input.type = "hidden"; input.name = "taskCodes"; input.value = code; codeCell.append(input);
        const nameCell = document.createElement("td"); nameCell.textContent = option.dataset.name;
        const actionCell = document.createElement("td"); const remove = document.createElement("button");
        remove.type = "button"; remove.className = "boton boton--secundario"; remove.dataset.directionRemove = ""; remove.textContent = "Quitar del plan";
        actionCell.append(remove); row.append(codeCell, nameCell, actionCell); selected.append(row);
        invalidateCapacity();
        updateSelection(); return true;
    }
    search.addEventListener("input", filter);
    search.addEventListener("keydown", event => {
        if (event.key !== "Enter") return;
        event.preventDefault();
        options.find(option => !option.hidden && !option.querySelector("button").disabled)?.querySelector("button").focus();
    });
    form.addEventListener("click", event => {
        const addButton = event.target.closest("[data-direction-add]");
        if (addButton) {
            const option = addButton.closest("[data-task-option]");
            if (add(option.dataset.code)) selectionStatus.textContent = `${option.dataset.name}. Agregada. ${selectedCodes().size} tareas seleccionadas.`;
            search.focus(); return;
        }
        const remove = event.target.closest("[data-direction-remove]");
        if (remove) {
            const row = remove.closest("[data-selected-code]"); const code = row.dataset.selectedCode;
            row.remove(); invalidateCapacity(); updateSelection(); selectionStatus.textContent = `${code}. ${selectedCodes().size} tareas seleccionadas.`;
            const option = catalog.get(code); if (option && !option.hidden) option.querySelector("button").focus(); else search.focus();
        }
    });
    const period = form.querySelector("#direction-period"), year = form.querySelector("#direction-year"), number = form.querySelector("#direction-number");
    const dates = form.querySelector("#direction-dates") || document.getElementById("direction-dates");
    function date(y, m, d) { const value = new Date(0); value.setUTCFullYear(y, m, d); value.setUTCHours(0, 0, 0, 0); return value; }
    function iso(value) {
        const thursday = new Date(value); thursday.setUTCDate(thursday.getUTCDate() + 4 - (thursday.getUTCDay() || 7));
        const y = thursday.getUTCFullYear(); const first = date(y, 0, 1);
        return { year: y, week: Math.ceil((((thursday - first) / 86400000) + 1) / 7) };
    }
    const formatted = value => `${String(value.getUTCDate()).padStart(2, "0")}/${String(value.getUTCMonth() + 1).padStart(2, "0")}/${String(value.getUTCFullYear()).padStart(4, "0")}`;
    function updateDates() {
        const p = period.value, y = Number(year.value), n = Number(number.value);
        form.querySelector("[data-direction-year-label]").textContent = p === "SEMANAL" ? "Año ISO" : "Año de calendario";
        const labels = { SEMANAL: "Semana ISO", MENSUAL: "Mes", TRIMESTRAL: "Trimestre", SEMESTRAL: "Semestre" };
        const max = { SEMANAL: 53, MENSUAL: 12, TRIMESTRAL: 4, SEMESTRAL: 2 };
        form.querySelector("[data-direction-number-field]").hidden = p === "ANUAL";
        form.querySelector("[data-direction-number-label]").textContent = labels[p] || "";
        number.required = p !== "ANUAL"; number.max = max[p] || 1;
        try {
            if (!Number.isInteger(y) || y < 1 || y > 9999 || p !== "ANUAL" && (!Number.isInteger(n) || n < 1 || n > max[p])) throw new Error();
            let start, end;
            if (p === "SEMANAL") {
                const jan4 = date(y, 0, 4); jan4.setUTCDate(jan4.getUTCDate() - (jan4.getUTCDay() + 6) % 7 + (n - 1) * 7);
                start = jan4; end = new Date(start); end.setUTCDate(end.getUTCDate() + 6);
                if (iso(start).year !== y || iso(start).week !== n) throw new Error();
            } else {
                const months = { MENSUAL: 1, TRIMESTRAL: 3, SEMESTRAL: 6, ANUAL: 12 }[p];
                if (!months) throw new Error();
                const firstMonth = p === "ANUAL" ? 0 : (n - 1) * months;
                start = date(y, firstMonth, 1); end = date(y, firstMonth + months, 0);
            }
            if (start.getUTCFullYear() < 1 || end.getUTCFullYear() > 9999) throw new Error();
            if (form.dataset.operations === "true") {
                const [ty, tm, td] = form.dataset.operationalToday.split("-").map(Number);
                const firstDay = date(ty, tm - 1, td);
                if (firstDay.getUTCDay() === 6) firstDay.setUTCDate(firstDay.getUTCDate() + 2);
                else if (firstDay.getUTCDay() === 0) firstDay.setUTCDate(firstDay.getUTCDate() + 1);
                if (end < firstDay) throw new Error();
                if (start < firstDay) start = firstDay;
            }
            const first = iso(start), last = iso(end);
            dates.textContent = `${formatted(start)} — ${formatted(end)}. Semana ${first.week}, año ISO ${first.year} — Semana ${last.week}, año ISO ${last.year}.`;
            year.setAttribute("aria-invalid", "false"); number.setAttribute("aria-invalid", "false");
        } catch {
            dates.textContent = "Revisa el nombre, el área y el periodo del plan";
            year.setAttribute("aria-invalid", "true"); number.setAttribute("aria-invalid", "true");
        }
    }
    period.addEventListener("change", () => {
        invalidateCapacity();
        number.value = period.value === "ANUAL" ? "" : "1"; updateDates();
        // Cancel any explicit load for the old period; changing the period never selects tasks.
        predefinedRequest++;
        predefined.disabled = false; predefined.setAttribute("aria-busy", "false"); predefinedStatus.textContent = "";
    });
    year.addEventListener("input", () => { invalidateCapacity(); updateDates(); }); number.addEventListener("input", () => { invalidateCapacity(); updateDates(); });
    const predefined = form.querySelector("[data-direction-predefined]"), predefinedStatus = form.querySelector("[data-predefined-status]");
    let predefinedRequest = 0;
    async function loadPredefined() {
        const requestId = ++predefinedRequest;
        const requestedPeriod = period.value;
        predefined.disabled = true; predefined.setAttribute("aria-busy", "true"); predefinedStatus.textContent = "Cargando tareas predefinidas…";
        try {
            const response = await fetch(`/api/v1/direction-plans/predefined-tasks?periodType=${encodeURIComponent(requestedPeriod)}`, { credentials: "same-origin", cache: "no-store" });
            if (response.status === 401) { window.location.assign("/acceso"); return; }
            if (response.status === 403) {
                selected.replaceChildren(); options.forEach(option => option.remove());
                form.querySelectorAll("input, select, button").forEach(control => { control.disabled = true; });
                predefinedStatus.textContent = "No tienes permiso para preparar o consultar estos planes"; return;
            }
            if (!response.ok) throw new Error();
            const result = await response.json();
            if (!Array.isArray(result.data) || result.data.some(task => !catalog.has(task.taskCode) || task.periodType !== requestedPeriod)) throw new Error();
            if (requestId !== predefinedRequest || requestedPeriod !== period.value) return;
            result.data.forEach(task => add(task.taskCode));
            predefinedStatus.textContent = result.data.length ? `${selectedCodes().size} tareas seleccionadas.` : "No hay tareas predefinidas configuradas para este periodo";
        } catch { if (requestId === predefinedRequest) predefinedStatus.textContent = "No se pudieron consultar los planes. Inténtalo de nuevo"; }
        finally { if (requestId === predefinedRequest) { if (form.querySelector('button[type="submit"]')?.disabled !== true) predefined.disabled = false; predefined.setAttribute("aria-busy", "false"); } }
    }
    predefined.addEventListener("click", loadPredefined);
    if (form.dataset.operations === "true") {
        ["direction-name", "direction-area", "direction-year", "direction-number"].forEach(id => { form.querySelector(`#${id}`).readOnly = true; });
        search.disabled = true; predefined.disabled = true;
        options.forEach(option => { option.querySelector("button").disabled = true; });
    }
    updateSelection(); updateDates(); filter();
    if (document.querySelector("[data-dialog-error]")) form.querySelectorAll("input, select, button").forEach(control => { control.disabled = true; });
})();
