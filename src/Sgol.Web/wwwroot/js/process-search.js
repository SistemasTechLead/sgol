(() => {
    const form = document.querySelector('[data-process-search]');
    if (!form) return;

    const key = 'sgol.process-search.scroll';
    // Consume once, and only restore the search navigation that saved this position.
    try {
        const saved = JSON.parse(sessionStorage.getItem(key) || 'null');
        sessionStorage.removeItem(key);
        if (saved && saved.url === location.href && Date.now() - saved.at < 30000 &&
            Number.isFinite(saved.x) && Number.isFinite(saved.y)) {
            const restore = () => {
                form.querySelector('[name="Search"]').focus({ preventScroll: true });
                window.scrollTo({ left: saved.x, top: saved.y, behavior: 'instant' });
            };
            if (document.readyState === 'complete') restore();
            else window.addEventListener('load', restore, { once: true });
        }
    } catch { /* Search remains usable when browser storage is unavailable. */ }

    form.addEventListener('submit', () => {
        const url = new URL(form.action);
        url.search = new URLSearchParams(new FormData(form)).toString();
        try {
            sessionStorage.setItem(key, JSON.stringify({
                url: url.href, x: window.scrollX, y: window.scrollY, at: Date.now()
            }));
        } catch { /* Keep the native GET submission as a fallback. */ }
    });
})();
