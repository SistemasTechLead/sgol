/* Catalog presentation only. Move original nodes, never clone successful fields. */
document.addEventListener('DOMContentLoaded', () => {
    'use strict';
    const catalog = document.querySelector('[data-task-catalog]');
    if (!catalog) return;
    const root = document.documentElement;
    const header = catalog.querySelector('.tasks-header-actions');
    const description = catalog.querySelector('.encabezado-pagina__descripcion');
    const panel = catalog.querySelector('.task-action-panel');
    const moves = [];
    let initialized = false, mobile = false, queued = false, management;
    const remember = (element, destination) => {
        if (!element || !destination) return;
        const home = document.createComment('catalog desktop position');
        element.before(home);
        moves.push({ element, home, parent: element.parentElement, destination });
    };
    const initialize = () => {
        const refresh = catalog.querySelector('.tasks-global-refresh');
        const settings = header?.querySelector('[data-presentation="tertiary"]');
        if (refresh || settings) {
            management = document.createElement('details');
            management.className = 'task-catalog-management';
            const summary = document.createElement('summary');
            const pending = Number(catalog.dataset.pendingRefresh) || 0;
            summary.textContent = pending ? `Gestión · ${pending} ${pending === 1 ? 'pendiente' : 'pendientes'}` : 'Gestión del catálogo';
            // Focusing the disclosure is not a request to reveal a hidden field.
            // Keep the shared invalid-field revealer from opening it before Enter.
            summary.addEventListener('focusin', event => event.stopPropagation());
            management.append(summary); header.append(management);
            remember(refresh, management);
            remember(catalog.querySelector('#global-refresh-status'), management);
            remember(settings, management);
        }
        remember(catalog.querySelector('[data-task-catalog-guidance]'), description);
        remember(catalog.querySelector('#task-selection-help'), description);
        const hint = panel?.querySelector('[data-task-selection-hint]');
        remember(hint, panel?.querySelector('.task-action-panel__buttons'));
        if (hint) {
            hint.id = 'task-assignment-hint';
            panel.querySelector('[data-task-assign]').setAttribute('aria-describedby', hint.id);
            const toggle = panel.querySelector('.mobile-task-actions-toggle');
            toggle?.setAttribute('aria-controls', `${toggle.getAttribute('aria-controls')} ${hint.id}`);
        }
        catalog.classList.add('task-catalog-refined');
        initialized = true;
    };
    const arrange = () => {
        queued = false;
        const narrow = !matchMedia('print').matches && root.getBoundingClientRect().width <= 48 * parseFloat(getComputedStyle(root).fontSize);
        if (!initialized) {
            if (!narrow) return;
            initialize();
        }
        if (narrow !== mobile) {
            const focused = document.activeElement;
            moves.forEach(({ element, home, parent, destination }) => {
                if (narrow) destination.append(element);
                // mobile.js may already have restored a navigation link.
                else if (element.parentElement !== parent) home.after(element);
            });
            mobile = narrow;
            if (!mobile && management) management.open = false;
            if (!mobile && focused === management?.querySelector('summary')) {
                (header.querySelector('.tasks-global-refresh button:not(:disabled)') || header.querySelector('[data-presentation="tertiary"]'))?.focus({ preventScroll: true });
            } else if (focused !== document.body && document.activeElement === document.body && focused.getClientRects().length) {
                focused.focus({ preventScroll: true });
            }
        }
        if (panel) panel.dataset.hasSelection = String(!!catalog.querySelector('.task-row--selected'));
    };
    const schedule = () => {
        if (queued) return;
        queued = true; requestAnimationFrame(arrange);
    };
    // mobile.js owns the shared controls and desktop navigation restoration;
    // arrange after its resize work, then only react to actual selection changes.
    new ResizeObserver(schedule).observe(root);
    new MutationObserver(schedule).observe(catalog, { subtree: true, attributes: true, attributeFilter: ['class'] });
    matchMedia('print').addEventListener('change', schedule);
    window.addEventListener('beforeprint', () => {
        moves.forEach(({element, home, parent}) => { if (element.parentElement !== parent) home.after(element); });
        mobile = false;
    });
    window.addEventListener('afterprint', schedule);
    schedule();
});
