(() => {
    "use strict";
    const form = document.querySelector("#report-filters");
    if (!form) return;
    const $ = selector => document.querySelector(selector);
    const initial = JSON.parse(form.dataset.configuration);
    const templatePicker = $("#report-template");
    const field = key => form.elements.namedItem("F." + key[0].toUpperCase() + key.slice(1));
    const numberKeys = new Set(["repetitions", "hours", "minutes", "progressMin", "progressMax"]);
    function config() {
        const result = {};
        for (const key of Object.keys(initial)) {
            const value = field(key)?.value ?? "";
            result[key] = numberKeys.has(key) ? (value === "" ? null : Number(value)) :
                (key === "from" || key === "to") && !value ? null : value;
        }
        const checked = [...document.querySelectorAll("[data-report-column]:checked")].map(input => input.value);
        result.columns = [...new Set([...field("columns").value.split(",").filter(key => checked.includes(key)), ...checked])].join(",");
        return result;
    }
    function dates() { $("#report-custom-dates").hidden = field("range").value !== "custom"; }
    function apply(value) {
        for (const key of Object.keys(initial)) {
            const input = field(key);
            if (input) {
                const saved = value[key] ?? "";
                // Retain a saved filter that no longer appears in the available options;
                // dropping it would silently widen the report to all records.
                if (input.tagName === "SELECT" && saved && ![...input.options].some(option => option.value === saved))
                    input.add(new Option(`${saved} (no disponible)`, saved));
                input.value = saved;
            }
        }
        document.querySelectorAll("[data-report-column]").forEach(input => input.checked = (value.columns ?? "").split(",").includes(input.value));
        dates();
    }
    function template() { return templatePicker.selectedOptions[0]; }
    function selectedConfig() {
        const option = template();
        if (option?.dataset.configuration) return JSON.parse(option.dataset.configuration);
        const kind = option?.value.startsWith("preset:") ? option.value.slice(7) : initial.kind;
        return { kind, range: "week", from: null, to: null, dateBasis: kind === "results" ? "reviewed" : kind === "overdue" ? "due" : kind === "review" ? "delivered" : "start",
            person: "", role: "", plan: "", area: "", task: "", specialization: "", status: "", requirement: "", schedule: "", frequency: "",
            repetitions: null, hours: null, minutes: null, progressMin: null, progressMax: null,
            group: kind === "plans" ? "plan" : "person", order: "due", columns: "task,person,plan,status,due,progress" };
    }
    function templateActions() {
        const personal = !!template()?.dataset.configuration && !templatePicker.value.startsWith("preset:");
        ["edit", "duplicate", "delete"].forEach(action => $("#report-" + action + "-template").disabled = !personal);
    }
    function customize() { $("#AppliedTemplate").value = ""; templatePicker.value = ""; templateActions(); }
    form.addEventListener("input", () => { customize(); $("#report-dirty").hidden = false; dates(); });
    form.addEventListener("change", () => { customize(); $("#report-dirty").hidden = false; dates(); });
    form.addEventListener("submit", event => {
        const value = config();
        if (!value.columns) { event.preventDefault(); field("columns").value = "task"; document.querySelector('[data-report-column][value="task"]').checked = true; form.requestSubmit(); return; }
        field("columns").value = value.columns;
    });
    templatePicker.addEventListener("change", templateActions);
    $("#report-apply-template").addEventListener("click", () => {
        if (!templatePicker.value) return; $("#EditingTemplate").value = ""; apply(selectedConfig()); $("#AppliedTemplate").value = templatePicker.value; form.requestSubmit();
    });
    function saveDialog(mode) {
        if (mode === "duplicate") { apply(selectedConfig()); $("#EditingTemplate").value = ""; }
        $("#report-save-title").textContent = mode === "edit" ? "Editar plantilla personal" : mode === "duplicate" ? "Duplicar plantilla personal" : "Guardar plantilla personal";
        const editing = mode === "new" && $("#EditingTemplate").value;
        if (editing) $("#report-save-title").textContent = "Guardar cambios en plantilla";
        $("#TemplateId").value = editing || "";
        $("#TemplateName").value = editing ? form.dataset.editingName : mode === "new" ? "" : template().textContent + " (copia)";
        const value = config(); if (!value.columns) value.columns = "task";
        $("#Configuration").value = JSON.stringify(value);
        $("#report-save-dialog").showModal(); $("#TemplateName").focus();
    }
    $("[data-report-save]").addEventListener("click", () => saveDialog("new"));
    $("#report-edit-template").addEventListener("click", () => { apply(selectedConfig()); $("#EditingTemplate").value = templatePicker.value; form.dataset.editingName = template().textContent; $("[data-report-save]").textContent = "Guardar cambios en plantilla"; $("#report-dirty").textContent = "Editando la plantilla. Ajusta los filtros y pulsa Guardar cambios en plantilla."; $("#report-dirty").hidden = false; $(".report-advanced").open = true; field("kind").focus(); });
    $("#report-duplicate-template").addEventListener("click", () => saveDialog("duplicate"));
    $("#report-delete-template").addEventListener("click", () => {
        $("#report-delete-name").textContent = "Se eliminará “" + template().textContent + "”.";
        $("#report-delete-form input[name=TemplateId]").value = templatePicker.value;
        $("#report-delete-dialog").showModal();
    });
    document.querySelectorAll("[data-report-close]").forEach(button => button.addEventListener("click", () => button.closest("dialog").close()));
    document.querySelectorAll("[data-report-status],[data-report-group]").forEach(button => button.addEventListener("click", () => {
        if (button.dataset.reportStatus) field("status").value = button.dataset.reportStatus;
        else if (button.dataset.reportGroup) {
            const input = field(initial.group);
            const option = input && [...input.options].find(option => option.value === button.dataset.reportGroup);
            if (!option) return;
            input.value = option.value;
        }
        customize(); form.requestSubmit();
    }));
    $("#report-search")?.addEventListener("input", event => {
        const search = window.SgolSearch.normalize(event.target.value); let shown = 0;
        document.querySelectorAll(".report-table tbody tr").forEach(row => { row.hidden = !window.SgolSearch.normalize(row.textContent).includes(search); if (!row.hidden) shown++; });
        $("#report-search-count").textContent = `${shown} asignaciones visibles en el detalle. Los indicadores corresponden al reporte generado.`;
    });
    apply(initial); templateActions();

    const snapshot = $("#report-result") ? JSON.parse($("#report-result").dataset.report) : null;
    const pdfDialog = $("#report-pdf-dialog");
    let pdf = null, url = null, mode = null;
    $("[data-report-export]").addEventListener("click", () => { if (snapshot) pdfDialog.showModal(); });
    function discard() {
        pdf = null; mode = null; $("#report-download").disabled = true;
        $("#report-pdf-preview").hidden = true; $("#report-pdf-preview").removeAttribute("src");
        if (url) { URL.revokeObjectURL(url); url = null; }
    }
    $("#report-pdf-mode").addEventListener("change", discard);
    pdfDialog.addEventListener("close", discard);
    $("#report-preview").addEventListener("click", async () => {
        const button = $("#report-preview"); button.disabled = true; discard();
        $("#report-pdf-status").textContent = "Preparando el documento…";
        try {
            const requestedMode = $("#report-pdf-mode").value;
            const result = await window.SgolReportPdf.create(snapshot, requestedMode);
            if (!pdfDialog.open || requestedMode !== $("#report-pdf-mode").value) return;
            pdf = result; mode = requestedMode; url = URL.createObjectURL(pdf.output("blob"));
            $("#report-pdf-preview").src = url; $("#report-pdf-preview").hidden = false;
            $("#report-download").disabled = false;
            $("#report-pdf-status").textContent = `${pdf.getNumberOfPages()} páginas listas. Revisa el documento y descárgalo cuando quieras.`;
        } catch (error) { console.error(error); $("#report-pdf-status").textContent = "No se pudo generar el PDF. Intenta nuevamente."; }
        finally { button.disabled = false; }
    });
    $("#report-download").addEventListener("click", () => {
        if (pdf) pdf.save(`reporte-${initial.kind}-${mode}.pdf`);
    });
})();

// Pure snapshot renderer: no additional API requests or recalculation of operational totals.
window.SgolReportPdf = {
    async create(data, mode = "summary") {
        if (!data?.title || !window.jspdf) throw new Error("Reporte no disponible");
        const doc = new window.jspdf.jsPDF({ orientation: mode === "full" ? "landscape" : "portrait", unit: "mm", format: "a4", compress: true });
        doc.setProperties({ title: data.title, subject: "Reporte operativo Loretta", author: "SGOL" });
        const image = new Image(); image.src = "/images/loretta.svg"; await image.decode();
        const canvas = document.createElement("canvas"); canvas.width = 1418; canvas.height = 388;
        const context = canvas.getContext("2d"); context.fillStyle = "#ffffff"; context.fillRect(0, 0, canvas.width, canvas.height); context.drawImage(image, 0, 0, canvas.width, canvas.height);
        const logo = canvas.toDataURL("image/png");
        const width = doc.internal.pageSize.getWidth(), height = doc.internal.pageSize.getHeight();
        const margin = 15, usable = width - margin * 2, bottom = height - 18;
        const palette = { info: [42,95,138], success: [47,122,79], danger: [178,58,46], warning: [146,96,11] };
        const clean = value => String(value ?? "").replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g, "").replace(/[–—]/g, "-").replace(/…/g, "...");
        let y = 0;
        function header() {
            doc.addImage(logo, "PNG", margin, 10, 45, 12.31);
            doc.setTextColor(107,96,85); doc.setFontSize(8); doc.text("SGOL | GESTIÓN OPERATIVA", width-margin, 15, { align: "right" });
            doc.setDrawColor(228,221,213); doc.line(margin, 27, width-margin, 27); y = 34;
        }
        function page() { doc.addPage(); header(); }
        function ensure(space) { if (y + space > bottom) page(); }
        function text(value, size = 9, color = [43,37,33], bold = false, indent = 0) {
            doc.setFont("helvetica", bold ? "bold" : "normal"); doc.setFontSize(size); doc.setTextColor(...color);
            const lines = doc.splitTextToSize(clean(value), usable - indent);
            const step = size * .43;
            for (const line of lines) { ensure(step + 1); doc.setFont("helvetica", bold ? "bold" : "normal"); doc.setFontSize(size); doc.setTextColor(...color); doc.text(line, margin+indent, y); y += step; }
            y += 2;
        }
        header(); text(data.title, 19, [43,37,33], true); text(data.description, 10, [107,96,85]);
        text(`Periodo: ${data.period} | Corte: ${data.cutoff} (Ciudad de México)`, 9);
        text(data.scope, 9); text(data.filters, 8, [107,96,85]); text(data.note, 8, [107,96,85]); y += 3;
        ensure(35);
        const cardWidth = (usable - 9) / 4;
        data.metrics.forEach((metric, i) => {
            const x = margin + i * (cardWidth+3);
            doc.setFillColor(246,241,236); doc.roundedRect(x,y,cardWidth,31,2,2,"F");
            doc.setFillColor(...palette[metric.tone] ?? palette.info); doc.rect(x,y,cardWidth,1.1,"F");
            doc.setFont("helvetica","normal"); doc.setFontSize(8); doc.setTextColor(43,37,33);
            doc.text(doc.splitTextToSize(clean(metric.label),cardWidth-6),x+3,y+6);
            doc.setFont("helvetica","bold"); doc.setFontSize(14); doc.setTextColor(...palette[metric.tone] ?? palette.info);
            let value = clean(metric.value); while (doc.getTextWidth(value) > cardWidth-6 && doc.getFontSize() > 9) doc.setFontSize(doc.getFontSize()-1);
            doc.text(value,x+3,y+15);
            doc.setFont("helvetica","normal"); doc.setFontSize(6.7); doc.setTextColor(107,96,85);
            doc.text(doc.splitTextToSize(clean(metric.hint),cardWidth-6),x+3,y+21);
        }); y += 39;
        for (const chart of data.charts) {
            ensure(45); text(`${chart.title} (${chart.unit})`,12,[43,37,33],true);
            const max = chart.unit === "%" ? 100 : Math.max(1,...chart.points.map(point => Math.max(point.value, point.capacity ?? 0)));
            if (!chart.points.length) text("Sin registros para esta gráfica.",9,[107,96,85]);
            for (const point of chart.points) {
                ensure(24); text(point.label,9,[43,37,33],true);
                ensure(point.capacity === null ? 13 : 16);
                doc.setFillColor(234,225,216); doc.roundedRect(margin,y,usable,3,1,1,"F");
                const tone = point.key === "CONCLUIDA" ? "success" : ["VENCIDA","NO_SATISFECHA"].includes(point.key) ? "danger" : point.key === "PENDIENTE_REVISION" ? "warning" : chart.tone;
                doc.setFillColor(...palette[tone] ?? palette.info);
                if (point.segments) { let x = margin; for (const segment of point.segments) { const segmentWidth = usable * segment.value / 100; doc.setFillColor(...palette[segment.tone]); if (segmentWidth > 0) doc.rect(x,y,segmentWidth,3,"F"); x += segmentWidth; } } else if (point.value > 0) doc.roundedRect(margin,y,usable*Math.min(1,point.value/max),3,1,1,"F"); y += 7;
                if (point.capacity != null) {
                    doc.setFillColor(133,96,43); if (point.capacity > 0) doc.rect(margin,y,usable*Math.min(1,point.capacity/max),1.8,"F"); y += 4;
                }
                text(point.caption,8,[107,96,85]); y += 2;
            }
            if (chart.points.some(point => point.segments)) text("Verde: concluidas. Rojo: vencidas o no satisfechas. Ámbar: por revisar. Azul: asignadas o programadas.",8,[107,96,85]);
            if (chart.unit === "minutos estimados") text("Azul: carga filtrada. Arena: capacidad configurada del periodo. Carga estimada, no tiempo real trabajado.",8,[107,96,85]);
            y += 5;
        }
        if (!data.rows.length) text("No hay tareas para esta consulta.",11,[107,96,85],true);
        if (mode === "full" && data.rows.length) {
            // Column bands keep wide custom reports readable; long rows continue on subsequent pages.
            for (let offset = 0; offset < data.columns.length; offset += 6) {
                const indices = Array.from({length: Math.min(6,data.columns.length-offset)},(_,i) => offset+i);
                if (offset > 0) indices.unshift(0);
                const columns = indices.map(i => data.columns[i]), cellWidth = usable/columns.length;
                page(); text(`Detalle del reporte | Columnas ${offset+1}-${Math.min(data.columns.length,offset+6)}`,13,[43,37,33],true);
                function tableHeader() {
                    const headers = columns.map(column => doc.splitTextToSize(clean(column.label),cellWidth-4));
                    const h = Math.max(...headers.map(lines => lines.length))*3.4+5;
                    ensure(h+5); doc.setFillColor(234,217,200); doc.rect(margin,y,usable,h,"F");
                    doc.setFont("helvetica","bold"); doc.setFontSize(8); doc.setTextColor(43,37,33);
                    headers.forEach((lines,i) => doc.text(lines,margin+i*cellWidth+2,y+4)); y += h;
                }
                tableHeader();
                for (const row of data.rows) {
                    doc.setFont("helvetica","normal"); doc.setFontSize(8);
                    const lines = indices.map(i => doc.splitTextToSize(clean(row[i]),cellWidth-4));
                    let remaining = Math.max(...lines.map(part => part.length)), lineOffset = 0;
                    while (remaining > 0) {
                        if (bottom-y < 10) { page(); tableHeader(); }
                        const take = Math.min(remaining,Math.floor((bottom-y-5)/3.5));
                        if (take < 1) { page(); tableHeader(); continue; }
                        const h = take*3.5+5;
                        doc.setFillColor(253,250,247); doc.rect(margin,y,usable,h,"F");
                        doc.setFont("helvetica","normal"); doc.setFontSize(8); doc.setTextColor(43,37,33);
                        lines.forEach((part,i) => {
                            let chunk = part.slice(lineOffset,lineOffset+take);
                            if (i === 0 && lineOffset > 0 && !chunk.length)
                                chunk = doc.splitTextToSize(clean(row[indices[0]]) + " (continuación)",cellWidth-4).slice(0,take);
                            if (chunk.length) doc.text(chunk,margin+i*cellWidth+2,y+4,{lineHeightFactor:1.24});
                        });
                        doc.setDrawColor(228,221,213); doc.line(margin,y+h,width-margin,y+h); y += h;
                        remaining -= take; lineOffset += take;
                        if (remaining > 0) { page(); tableHeader(); }
                    }
                }
            }
        }
        const pages = doc.getNumberOfPages();
        for (let i=1;i<=pages;i++) {
            doc.setPage(i); doc.setFont("helvetica","normal"); doc.setFontSize(7); doc.setTextColor(107,96,85);
            doc.text(`Loretta | ${clean(data.title)} | Corte: ${data.cutoff}`,margin,height-9);
            doc.text(`${i} / ${pages}`,width-margin,height-9,{align:"right"});
        }
        return doc;
    }
};



