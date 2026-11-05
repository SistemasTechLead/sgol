/* Desktop presentation only. Original controls, form owners and values are retained. */
(() => {
    'use strict';
    if (!document.body.matches('[data-desktop-workspace="true"]')) return;
    const root = document.documentElement;
    const main = document.querySelector('#contenido-principal');
    const filterSelector = '.supervision-filters, .tabla-filtros, .people-filters, .tasks-search, .board-plan, #report-filters, .report-detail-heading form, form[data-desktop-filters], form.bonus-week';
    const wrappers = new Set();
    let desktop = false;
    let printing = false;

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
