(() => {
    'use strict';
    const handle = document.querySelector('[data-sidebar-resize]');
    if (!handle) return;
    const root = document.documentElement;
    const sidebar = handle.closest('.barra-lateral');
    const storageKey = 'sgol.appearance.sidebarWidth';
    const min = 72, max = 224;
    let drag = null;
    const currentWidth = () => parseFloat(getComputedStyle(root).getPropertyValue('--ancho-navegacion')) || max;
    const apply = width => {
        width = Math.round(Math.min(max, Math.max(min, width)));
        root.style.setProperty('--ancho-navegacion', `${width}px`);
        root.dataset.sidebarCompact = String(width < 180);
        handle.setAttribute('aria-valuenow', String(width));
        handle.setAttribute('aria-valuetext', width < 180 ? `Solo iconos, ${width} píxeles` : `${width} píxeles`);
    };
    const save = () => {
        try { localStorage.setItem(storageKey, String(currentWidth())); } catch { /* Storage is optional. */ }
    };
    const finish = (cancel = false) => {
        if (!drag) return;
        const { id, width } = drag;
        drag = null;
        root.classList.remove('ajustando-barra-lateral');
        if (cancel) apply(width);
        else save();
        if (handle.hasPointerCapture(id)) handle.releasePointerCapture(id);
    };
    handle.addEventListener('pointerdown', event => {
        if (!event.isPrimary || event.button !== 0 || drag) return;
        event.preventDefault();
        handle.focus({ preventScroll: true });
        drag = { id: event.pointerId, x: event.clientX, width: sidebar.getBoundingClientRect().width };
        handle.setPointerCapture(event.pointerId);
        root.classList.add('ajustando-barra-lateral');
    });
    handle.addEventListener('pointermove', event => {
        if (drag && event.pointerId === drag.id) apply(drag.width + event.clientX - drag.x);
    });
    handle.addEventListener('pointerup', event => { if (drag?.id === event.pointerId) finish(); });
    handle.addEventListener('pointercancel', () => finish(true));
    handle.addEventListener('lostpointercapture', () => finish(true));
    handle.addEventListener('keydown', event => {
        if (event.key === 'Escape' && drag) { finish(true); event.preventDefault(); return; }
        const width = currentWidth();
        const next = { ArrowLeft: width - 16, ArrowRight: width + 16, Home: min, End: max }[event.key];
        if (next === undefined) return;
        event.preventDefault();
        apply(next);
        save();
    });
    window.addEventListener('blur', () => finish(true));
    window.addEventListener('storage', event => {
        if ((event.key !== storageKey && event.key !== null) || drag) return;
        const value = Number(event.key === null ? null : event.newValue);
        apply(Number.isFinite(value) && value >= min && value <= max ? value : max);
    });
    apply(currentWidth());
})();
