/* Desktop presentation only. Original controls, form owners and values are retained. */
(() => {
    'use strict';
    if (!document.body.matches('[data-desktop-workspace="true"]')) return;
    const root = document.documentElement;
    const main = document.querySelector('#contenido-principal');
    const titleHost = document.querySelector('.desktop-screen-heading');
    const infoButton = document.querySelector('.desktop-screen-info');
    const infoPanel = document.querySelector('#desktop-screen-help');
    const informationSelector = '.encabezado-pagina__descripcion, .encabezado-pagina__contenido > p, [data-desktop-information], .my-work-guide, .my-work-desktop-legend, #task-filter-calendar-help, [data-task-catalog-guidance], #task-selection-help';
    // Mobile catalog presentation can nest its guidance inside the description.
    const informationSources = () => [...main.querySelectorAll(informationSelector)]
        .filter(element => !element.parentElement.closest(informationSelector));
    const filterSelector = '.supervision-filters, .tabla-filtros, .people-filters, .tasks-search, .board-plan, #report-filters, .report-detail-heading form, form[data-desktop-filters], form.bonus-week';
    const wrappers = new Set();
    let desktop = false;
    let printing = false;

    function closeInformation(returnFocus = false) {
        infoPanel.hidden = true;
        infoButton.setAttribute('aria-expanded', 'false');
        if (returnFocus) infoButton.focus({ preventScroll: true });
    }
    function positionInformation() {
        if (infoPanel.hidden) return;
        const anchor = infoButton.getBoundingClientRect();
        if (anchor.bottom < 0 || anchor.top > innerHeight) { closeInformation(); return; }
        const bounds = root.getBoundingClientRect();
        const width = Math.min(380, bounds.width - 32);
        infoPanel.style.width = `${width}px`;
        infoPanel.style.left = `${Math.max(bounds.left + 16, Math.min(anchor.left, bounds.right - width - 16))}px`;
        infoPanel.style.top = `${anchor.bottom + 8}px`;
        infoPanel.style.maxHeight = `${Math.max(80, innerHeight - anchor.bottom - 24)}px`;
    }
    infoButton.addEventListener('click', () => {
        if (!infoPanel.hidden) { closeInformation(); return; }
        infoPanel.replaceChildren(...informationSources().map(source => {
            const copy = source.cloneNode(true);
            [copy, ...copy.querySelectorAll('*')].forEach(node => {
                node.removeAttribute('id'); node.removeAttribute('data-desktop-redundant');
                node.removeAttribute('style');
                node.classList.remove('desktop-information-source', 'mobile-help-content', 'mobile-help-open');
            });
            return copy;
        }));
        infoPanel.hidden = false;
        infoButton.setAttribute('aria-expanded', 'true');
        positionInformation();
    });
    document.addEventListener('click', event => {
        if (!infoPanel.hidden && !infoPanel.contains(event.target) && !infoButton.contains(event.target)) closeInformation();
    });
    window.addEventListener('scroll', positionInformation, true);

    function disclosure(element, label, className) {
        const home = document.createComment('desktop disclosure original position');
        element.before(home);
        const details = document.createElement('details');
        details.className = className;
        const summary = document.createElement('summary');
        summary.textContent = label;
        details.append(summary);
        home.after(details);
        details.append(element);
        wrappers.add({ details, element, home });
        return { details, summary };
    }

    function enhanceHeading() {
        const source = [...main.querySelectorAll('h1')].find(h => !h.closest('dialog'));
        if (source) {
            source.classList.add('desktop-source-title');
            // aria-labelledby on the original section keeps its original target.
            source.closest('header')?.classList.add('desktop-page-heading');
        }
        const information = informationSources();
        information.forEach(element => element.classList.add('desktop-information-source'));
        infoButton.hidden = information.length === 0;
        positionInformation();
    }

    function enhance() {
        wrappers.forEach(entry => {
            if (!entry.home.isConnected) { entry.details._dispose?.(); wrappers.delete(entry); }
        });
        main.querySelectorAll(filterSelector).forEach(form => {
            if (form.dataset.desktopFiltersReady) return;
            if (window.SgolSearch.isStandalone(form)) {
                form.classList.add('standalone-search');
                return;
            }
            form.dataset.desktopFiltersReady = 'true';
            form.classList.add('desktop-filter-bar');
            form.querySelectorAll('.report-filter-grid').forEach(grid => grid.classList.add('desktop-filter-group'));
            const fields = [...form.querySelectorAll('.campo, .board-filter, .desktop-filter-field')].filter(field =>
                (!field.closest('details') || !form.contains(field.closest('details'))) && !field.parentElement.closest('.campo, .board-filter, .desktop-filter-field'));
            fields.forEach(field => {
                const control = field.querySelector('input:not([type="hidden"]), select');
                if (!control) return;
                const label = (form.matches('.bonus-week') ? 'Semana' : field.querySelector('.campo__etiqueta, label')?.textContent || control.labels?.[0]?.textContent || control.name).trim();
                const focused = field.contains(document.activeElement) ? document.activeElement : null;
                const { details, summary } = disclosure(field, label, 'desktop-filter');
                const refresh = () => {
                    const value = control.tagName === 'SELECT'
                        ? [...control.selectedOptions].filter(o => o.value).map(o => o.textContent.trim()).join(', ')
                        : control.value;
                    summary.textContent = value ? `${label}: ${value}` : label;
                };
                refresh();
                control.addEventListener('input', refresh);
                control.addEventListener('change', refresh);
                form.addEventListener('sgol:filters-updated', refresh);
                // Remove listeners when restoring the original mobile/print form.
                details._dispose = () => { control.removeEventListener('input', refresh); control.removeEventListener('change', refresh); form.removeEventListener('sgol:filters-updated', refresh); };
                if (field.querySelector('[aria-invalid="true"], .field-validation-error')) details.open = true;
                if (focused) { details.open = true; focused.focus({ preventScroll: true }); }
            });
        });
    }

    function restore() {
        wrappers.forEach(({ details, element, home }) => {
            details._dispose?.();
            if (home.isConnected) home.replaceWith(element);
            details.remove();
        });
        wrappers.clear();
        main.querySelectorAll('[data-desktop-filters-ready]').forEach(form => {
            delete form.dataset.desktopFiltersReady;
            form.classList.remove('desktop-filter-bar');
        });
        main.querySelectorAll('.desktop-filter-group').forEach(grid => grid.classList.remove('desktop-filter-group'));
    }

    function synchronize() {
        const next = !printing && !matchMedia('print').matches && root.getBoundingClientRect().width > 48 * parseFloat(getComputedStyle(root).fontSize);
        if (!next && desktop) restore();
        desktop = next;
        document.body.classList.toggle('desktop-workspace', desktop);
        const screen = !printing && !matchMedia('print').matches;
        titleHost.hidden = !screen;
        document.body.classList.toggle('workspace-heading-ready', screen);
        if (!screen) closeInformation();
        if (screen) enhanceHeading();
        if (desktop) enhance();
    }
    // Native validation must expose a collapsed invalid field before focusing it.
    document.addEventListener('invalid', event => {
        if (!desktop) return;
        for (let parent = event.target.parentElement; parent && parent !== main; parent = parent.parentElement)
            if (parent.matches('details')) parent.open = true;
    }, true);
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        if (!infoPanel.hidden) { closeInformation(true); event.preventDefault(); return; }
        if (!desktop) return;
        const details = event.target.closest('.desktop-filter[open], .desktop-guide[open]');
        if (details) { details.open = false; details.querySelector('summary').focus(); event.preventDefault(); }
    });
    document.addEventListener('click', event => {
        if (desktop && event.target.closest('#report-edit-template')) {
            const field = main.querySelector('[name="F.Kind"]');
            const details = field?.closest('.desktop-filter');
            if (details) details.open = true;
        }
    }, true);
    let boardState = [];
    main.addEventListener('sgol:before-board-refresh', event => {
        boardState = [...event.target.querySelectorAll('.desktop-filter')].map(details => ({
            key: details.querySelector('select')?.id, open: details.open,
            focus: details.contains(document.activeElement) ? (document.activeElement.matches('summary') ? 'summary' : 'select') : null
        }));
    });
    main.addEventListener('sgol:board-refreshed', () => {
        synchronize();
        if (desktop) boardState.forEach(state => {
            const control = document.getElementById(state.key);
            const details = control?.closest('.desktop-filter');
            if (!details) return;
            details.open = state.open;
            if (state.focus) details.querySelector(state.focus).focus({ preventScroll: true });
        });
        boardState = [];
    });
    let frame = 0;
    const schedule = () => { if (!frame) frame = requestAnimationFrame(() => { frame = 0; synchronize(); }); };
    new ResizeObserver(schedule).observe(root);
    new MutationObserver(mutations => {
        if (mutations.some(m => [...m.addedNodes].some(n => n.nodeType === 1 && (n.matches('.board-content') || n.querySelector('.board-content'))))) schedule();
    }).observe(main, { childList: true, subtree: true });
    window.addEventListener('beforeprint', () => { printing = true; synchronize(); });
    window.addEventListener('afterprint', () => { printing = false; synchronize(); });
    matchMedia('print').addEventListener('change', schedule);
    synchronize();
})();
