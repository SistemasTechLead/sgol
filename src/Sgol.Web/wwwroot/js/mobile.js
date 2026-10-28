/* Presentation enhancement only: no requests, business state or form values. */
(() => {
    'use strict';
    const root = document.documentElement;
    const states = new Map();
    let nextId = 0;
    const identity = element => {
        if (!element.id) {
            let id; do { id = `mobile-view-${++nextId}`; } while (document.getElementById(id));
            element.id = id;
        }
        return element.id;
    };
    const isMobile = () => root.getBoundingClientRect().width <= 48 * parseFloat(getComputedStyle(root).fontSize);
    const hasError = element => !!element.querySelector('.field-validation-error, .validation-summary-errors, [aria-invalid="true"], [role="alert"]');

    function fold(element, label, key, buttonHost) {
        if (!element || element.dataset.mobileFold) return null;
        element.dataset.mobileFold = key;
        const button = document.createElement('button');
        button.type = 'button'; button.className = 'mobile-fold-toggle'; button.textContent = label;
        button.dataset.mobileControl = key;
        button.setAttribute('aria-controls', identity(element));
        element.classList.add('mobile-fold-target');
        const update = open => {
            element.classList.toggle('mobile-fold-open', open);
            button.setAttribute('aria-expanded', String(open));
        };
        update(hasError(element) || element.contains(document.activeElement) || states.get(key) === true);
        button.addEventListener('click', () => {
            const open = !element.classList.contains('mobile-fold-open');
            states.set(key, open); update(open);
        });
        // Existing scripts may focus a control (e.g. Edit template). Reveal it
        // before the browser attempts to focus, without changing its behaviour.
        element.addEventListener('focusin', () => { if (isMobile()) update(true); });
        if (buttonHost) buttonHost.append(button); else element.before(button);
        return button;
    }

    function foldGroup(elements, label, key, anchor) {
        elements = elements.filter(Boolean);
        if (!elements.length || elements.some(e => e.dataset.mobileFold)) return;
        const button = document.createElement('button'); button.type = 'button';
        button.className = 'mobile-fold-toggle'; button.textContent = label; button.dataset.mobileControl = key;
        button.setAttribute('aria-controls', elements.map(identity).join(' '));
        elements.forEach(e => { e.dataset.mobileFold = key; e.classList.add('mobile-fold-target'); });
        const update = open => {
            elements.forEach(e => e.classList.toggle('mobile-fold-open', open));
            button.setAttribute('aria-expanded', String(open));
        };
        update(elements.some(hasError) || elements.some(e => e.contains(document.activeElement)) || states.get(key) === true);
        button.addEventListener('click', () => { const open = button.getAttribute('aria-expanded') !== 'true'; states.set(key, open); update(open); });
        (anchor || elements[0]).before(button);
        return button;
    }

    function filters() {
        document.querySelectorAll('.supervision-filters').forEach((form, index) => {
            const section = form.closest('section');
            const active = [...form.querySelectorAll('input:not([type="hidden"]), select')].filter(c => c.value !== '').length;
            const header = section?.querySelector(':scope > header');
            const host = header && getComputedStyle(header).display !== 'none' ? header : null;
            fold(form, active ? `Filtros · ${active} activos` : 'Filtros', `supervision-${section?.id || index}`, host);
        });
        const controls = document.querySelector('.report-controls');
        const reportForm = document.querySelector('#report-filters');
        let reportLabel = 'Filtros y plantillas';
        if (reportForm) {
            const kind = reportForm.elements.namedItem('F.Kind');
            const range = reportForm.elements.namedItem('F.Range');
            reportLabel = `Filtros · ${kind?.selectedOptions[0]?.textContent || ''} · ${range?.selectedOptions[0]?.textContent || ''}`;
        }
        fold(controls, reportLabel, 'reports');
        fold(document.querySelector('.report-template-picker'), 'Plantillas', 'report-templates');
        const boardForm = document.querySelector('.board-plan');
        if (boardForm) foldGroup([boardForm, document.querySelector('.board-heading > .board-action'), document.querySelector('.board-context > span')], 'Filtros', 'board-filters', boardForm);
        const peopleButton = fold(document.querySelector('.board-collaborator-progress'), 'Personas', 'board-people', document.querySelector('.board-header'));
        peopleButton?.setAttribute('aria-label', 'Progreso por persona');
        const peopleFilters = document.querySelector('.people-filters');
        fold(peopleFilters, 'Filtros', 'people-filters', peopleFilters?.closest('section')?.querySelector('header'));
        fold(document.querySelector('.tasks-search'), 'Buscar y filtrar tareas', 'task-search');
        document.querySelectorAll('.planificacion__navigation-header .planificacion__header-actions').forEach((nav, i) => {
            const items = [...nav.children].filter(e => !e.matches('.mobile-fold-toggle'));
            if (items.length > 2) foldGroup(items.slice(1), 'Más opciones', `planning-actions-${i}`);
        });
        const reportSummary = document.querySelector('.report-filter-summary');
        if (reportSummary) {
            const button = foldGroup([...document.querySelectorAll('.report-result-heading p, .report-eyebrow, .report-period > span'), reportSummary], 'Detalle', 'report-context', reportSummary);
            if (button) { button.setAttribute('aria-label', 'Detalle de la consulta'); document.querySelector('.report-period')?.append(button); }
        }
        const counts = document.querySelector('.people-counts');
        const policy = document.querySelector('.people-page > p[role="status"]');
        foldGroup([policy, counts], 'Resumen de usuarios', 'people-summary', counts);
        // Keep Configuration and My work as the established visual references.
        if (!location.pathname.startsWith('/configuracion') && !location.pathname.startsWith('/mi-trabajo')) {
            document.querySelectorAll('main > .encabezado-pagina .encabezado-pagina__descripcion, main > section > .encabezado-pagina .encabezado-pagina__descripcion')
                .forEach((p, i) => {
                    const button = fold(p, 'i', `page-info-${i}`);
                    if (button) { button.classList.add('mobile-info-toggle'); button.setAttribute('aria-label', 'Información de la página'); p.parentElement.classList.add('mobile-heading'); }
                });
        }
    }

    function sectionNavigation() {
        const sections = [...document.querySelectorAll('section.supervision-section[id]')];
        if (sections.length < 2 || document.querySelector('.mobile-section-nav')) return;
        const nav = document.createElement('nav'); nav.className = 'mobile-section-nav'; nav.setAttribute('aria-label', 'Apartados de supervisión');
        const label = document.createElement('label'); label.textContent = 'Ir a un apartado';
        const select = document.createElement('select'); select.className = 'campo__control';
        select.add(new Option('Seleccionar apartado', ''));
        sections.forEach(section => select.add(new Option(section.querySelector('h2')?.textContent.trim(), section.id)));
        select.addEventListener('change', () => {
            const section = document.getElementById(select.value); if (!section) return;
            section.tabIndex = -1; section.focus({ preventScroll: true }); section.scrollIntoView({ block: 'start' });
        });
        label.append(select); nav.append(label);
        const capacity = document.querySelector('#capacity');
        if (capacity) select.add(new Option('Carga de trabajo por persona', capacity.id));
        (capacity || sections[0]).before(nav);
    }

    function rows(table) {
        const head = table.tHead?.rows[0];
        if (!head || !table.tBodies.length) return;
        const weekly = table.classList.contains('weekly-table');
        const headers = [...head.cells];
        const expandedHeaders = headers.flatMap(h => Array(h.colSpan).fill(h));
        const editable = table.classList.contains('direction-capacity__table');
        const bodyRows = [...table.tBodies, ...(table.tFoot ? [table.tFoot] : [])].flatMap(b => [...b.rows]);
        if (!bodyRows.length || table.querySelector('.my-work-task')) return;
        // Unknown merged layouts remain a local scroller, never guessed columns.
        if (!weekly && bodyRows.some(row => [...row.cells].some(c => c.colSpan > 1 || c.rowSpan > 1))) return;
        if (!table.dataset.mobileRecords) {
            table.dataset.mobileRecords = 'true'; table.classList.add('mobile-records');
            table.setAttribute('role', 'table'); identity(table);
            table.tHead.setAttribute('role', 'rowgroup'); head.setAttribute('role', 'row');
            headers.forEach(h => { identity(h); h.setAttribute('role', 'columnheader'); h.scope = 'col'; });
            [...table.tBodies].forEach(b => b.setAttribute('role', 'rowgroup'));
            table.tFoot?.setAttribute('role', 'rowgroup');
            table.parentElement.classList.add('mobile-table-container');
            if (weekly) dayPicker(table, expandedHeaders);
        }
        bodyRows.forEach(row => {
            if (row.dataset.mobileRecord) return;
            row.dataset.mobileRecord = 'true'; row.setAttribute('role', 'row');
            const cells = [...row.cells]; let offset = 0;
            const identityIndex = Math.max(0, headers.findIndex(h => /^(Tarea|Nombre|Perfil|Actividad|Trabajador|Persona)/i.test(h.textContent.trim())));
            const secondary = [];
            cells.forEach((cell, index) => {
                const header = expandedHeaders[offset]; const label = header?.textContent.trim() || '';
                const headerIds = [...new Set(expandedHeaders.slice(offset, offset + cell.colSpan).map(h => h.id))];
                cell.dataset.mobileLabel = label;
                if (headerIds.length) cell.setAttribute('headers', headerIds.join(' '));
                cell.setAttribute('role', cell.tagName === 'TH' ? 'rowheader' : 'cell');
                if (index === identityIndex) cell.classList.add('mobile-record-title');
                const actions = !!cell.querySelector('a[href], button, input:not([type="hidden"]), select, textarea');
                if (actions && !weekly) cell.classList.add('mobile-record-actions');
                if (weekly && offset >= 2) {
                    cell.dataset.mobileDay = String(Math.floor((offset - 2) / 2));
                    cell.classList.toggle('mobile-day-hidden', table.dataset.mobileDaySelection !== '' && cell.dataset.mobileDay !== table.dataset.mobileDaySelection);
                }
                if (!weekly && !editable && cells.length > 3 && index !== identityIndex && !actions &&
                    !/^(Estado|Responsable|Trabajador|Vencimiento|Inicio\s*\/|Fechas)/i.test(label)) {
                    cell.classList.add('mobile-record-secondary'); identity(cell); secondary.push(cell);
                }
                offset += cell.colSpan;
            });
            if (secondary.length) {
                const toggle = document.createElement('button'); toggle.type = 'button'; toggle.className = 'mobile-record-toggle';
                toggle.setAttribute('aria-label', `Mostrar más datos de ${cells[identityIndex].textContent.trim().slice(0, 120)}`);
                toggle.setAttribute('aria-expanded', 'false'); toggle.setAttribute('aria-controls', secondary.map(c => c.id).join(' '));
                toggle.addEventListener('click', () => {
                    const open = row.classList.toggle('mobile-record-expanded');
                    toggle.setAttribute('aria-expanded', String(open));
                    toggle.setAttribute('aria-label', `${open ? 'Ocultar' : 'Mostrar'} datos adicionales del registro`);
                });
                // No added text nodes: existing text-based table searches keep
                // matching only the original data. The label is visual CSS + ARIA.
                (cells.find(c => c.classList.contains('mobile-record-actions') && !c.classList.contains('mobile-record-title')) || cells[identityIndex]).append(toggle);
            }
        });
    }

    function dayPicker(table, headers) {
        // The weekly matrix keeps its data and desktop layout. On mobile a
        // day selector presents the same cells vertically, without a new query.
        const label = document.createElement('label'); label.className = 'mobile-day-picker'; label.textContent = 'Consultar día';
        const select = document.createElement('select'); select.className = 'campo__control'; select.add(new Option('Todos los días', ''));
        const key = table.classList.contains('weekly-workload-table') ? 'weekly-day-load' : 'weekly-day-tasks';
        select.dataset.mobileControl = key; select.setAttribute('aria-controls', table.id);
        for (let i = 2; i < headers.length; i += 2) select.add(new Option(headers[i].textContent.trim(), String((i - 2) / 2)));
        select.value = states.get(key) ?? ''; table.dataset.mobileDaySelection = select.value;
        select.addEventListener('change', () => {
            states.set(key, select.value); table.dataset.mobileDaySelection = select.value;
            table.querySelectorAll('[data-mobile-day]').forEach(cell => {
                cell.classList.toggle('mobile-day-hidden', select.value !== '' && cell.dataset.mobileDay !== select.value);
            });
        });
        label.append(select); table.parentElement.before(label);
    }

    function enhance() {
        filters(); sectionNavigation();
        document.querySelectorAll('table.tabla, table.report-table, table.weekly-table').forEach(rows);
    }

    // Capacity is secondary to the review queue. Move the same element (never a
    // copy of its forms) and restore its exact desktop position on width change.
    let initialized = false, capacity = null, capacityHome = null, reportSave = null, reportSaveHome = null;
    const arrange = () => {
        const mobile = isMobile();
        if (reportSave) {
            const controls = document.querySelector('.report-controls');
            if (mobile && controls && (reportSave.parentElement !== controls || controls.lastElementChild !== reportSave)) controls.append(reportSave);
            else if (!mobile && reportSave.previousSibling !== reportSaveHome) reportSaveHome.after(reportSave);
        }
        if (!capacity) return;
        const sections = [...document.querySelectorAll('section.supervision-section')];
        if (mobile && !capacity.open && sections.length && capacity.previousElementSibling !== sections.at(-1)) sections.at(-1).after(capacity);
        else if (!mobile && capacity.previousSibling !== capacityHome) capacityHome.after(capacity);
    };

    // A closed <details> remains submitted normally. Invalid controls must be
    // revealed synchronously before native form validation tries to focus them.
    const reveal = element => {
        for (let parent = element.parentElement; parent; parent = parent.parentElement) {
            if (parent.matches('details')) parent.open = true;
            if (parent.matches('.mobile-fold-target')) {
                parent.classList.add('mobile-fold-open');
                document.querySelector(`[aria-controls~="${parent.id}"]`)?.setAttribute('aria-expanded', 'true');
            }
            if (parent.matches('.mobile-record-secondary')) {
                const row = parent.closest('tr'); row?.classList.add('mobile-record-expanded');
                row?.querySelector('.mobile-record-toggle')?.setAttribute('aria-expanded', 'true');
            }
        }
    };
    document.addEventListener('invalid', event => { if (isMobile()) reveal(event.target); }, true);
    // Reveal the reports editor before its existing click listener focuses it.
    document.addEventListener('click', event => {
        if (event.target.closest('#report-edit-template')) {
            const form = document.querySelector('#report-filters'); if (form) reveal(form);
        }
    }, true);
    let lastFocused = null;
    document.addEventListener('focusin', event => { lastFocused = event.target; if (isMobile()) reveal(event.target); });

    let queued = false;
    const observer = new MutationObserver(mutations => {
        // Only inserted structures matter, not timers, progress text or styles.
        if (queued || !mutations.some(m => [...m.addedNodes].some(n => n.nodeType === 1 &&
            !n.matches('.mobile-fold-toggle, .mobile-record-toggle, .mobile-section-nav, .mobile-day-picker') &&
            (n.matches('table, tr, .board-content, .supervision-filters') || n.querySelector('table, .board-plan, .supervision-filters'))))) return;
        queued = true; queueMicrotask(() => {
            queued = false;
            const restoreKey = lastFocused && !lastFocused.isConnected ? lastFocused.dataset.mobileControl : null;
            enhance();
            if (restoreKey && document.activeElement === document.body)
                [...document.querySelectorAll('[data-mobile-control]')].find(e => e.dataset.mobileControl === restoreKey)?.focus({ preventScroll: true });
        });
    });
    const synchronizeView = () => {
        // Leave desktop DOM untouched, and avoid scanning its large task tables.
        // Enhancement starts only when the actual chosen container is mobile.
        if (!initialized) {
            if (!isMobile()) return;
            initialized = true;
            capacity = document.querySelector('#capacity');
            if (capacity) { capacityHome = document.createComment('capacity desktop position'); capacity.before(capacityHome); }
            reportSave = document.querySelector('[data-report-save]');
            if (reportSave) { reportSaveHome = document.createComment('report save desktop position'); reportSave.before(reportSaveHome); }
            enhance(); observer.observe(document.body, { subtree: true, childList: true });
        }
        arrange();
    };
    // Resize notifications must not move observed content in the same delivery cycle.
    // Coalesce changes and only relocate nodes when their destination actually changes.
    let viewFrame = 0;
    const scheduleView = () => {
        if (viewFrame) return;
        viewFrame = requestAnimationFrame(() => { viewFrame = 0; synchronizeView(); });
    };
    synchronizeView(); new ResizeObserver(scheduleView).observe(root);
})();
