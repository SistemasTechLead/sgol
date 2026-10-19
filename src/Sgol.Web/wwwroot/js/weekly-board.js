(() => {
    const root = document.getElementById('weekly-board');
    const notice = document.getElementById('weekly-connection');
    if (!root || !notice) return;
    let running = false, stopped = false;
    async function refresh() {
        if (running || stopped || document.hidden) return;
        running = true;
        root.setAttribute('aria-busy', 'true');
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);
        try {
            const url = new URL(location.href);
            url.searchParams.set('handler', 'Refresh');
            const response = await fetch(url, { cache: 'no-store', credentials: 'same-origin', signal: controller.signal });
            if (response.redirected || response.status === 401 || response.status === 403) {
                if (document.body.dataset.screen === 'true') { location.replace('/pantalla'); return; }
                stopped = true; root.replaceChildren();
                notice.replaceChildren(document.createTextNode('La sesión terminó o ya no tienes acceso. '));
                const link = document.createElement('a'); link.href = '/acceso'; link.textContent = 'Iniciar sesión'; notice.append(link);
                notice.hidden = false; return;
            }
            if (!response.ok) throw new Error('refresh');
            const template = document.createElement('template');
            template.innerHTML = await response.text();
            if (!template.content.querySelector('.weekly-table')) throw new Error('content');
            const viewport = root.querySelector('.weekly-viewport');
            const top = viewport.scrollTop, left = viewport.scrollLeft;
            const focused = root.contains(document.activeElement) ? document.activeElement : null;
            const href = focused?.closest('a')?.getAttribute('href');
            root.replaceChildren(template.content);
            const next = root.querySelector('.weekly-viewport');
            next.scrollTop = top; next.scrollLeft = left;
            if (focused?.matches('.weekly-viewport')) next.focus({ preventScroll: true });
            else if (href) [...root.querySelectorAll('a')].find(a => a.getAttribute('href') === href)?.focus({ preventScroll: true });
            notice.hidden = true;
        } catch {
            notice.textContent = 'Sin actualización. Mostrando los últimos datos recibidos; reintentando conexión.';
            notice.hidden = false;
        } finally {
            clearTimeout(timeout); running = false; root.setAttribute('aria-busy', 'false');
        }
    }
    setInterval(refresh, 15000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
    window.addEventListener('online', refresh);
})();
