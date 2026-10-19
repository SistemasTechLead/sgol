(() => {
    const root = document.getElementById('live-board');
    const notice = document.getElementById('board-connection');
    if (!root || !notice) return;
    let serverAt = 0, syncedAt = 0, refreshing = false, stopped = false;
    let pauseUntil = 0, scrollDirection = 1, scrollWaitUntil = performance.now() + 7000;
    let retryAfter = 0;
    let events = null, eventPlan = null, realtime = false, dirty = false;
    const liveStatus = () => {
        const label = root.querySelector('[data-live-status]');
        if (label) label.textContent = realtime ? '● En vivo' : 'Actualización automática';
    };
    function expire() {
        if (document.body.dataset.screen === 'true') { location.replace('/pantalla'); return; }
        stopped = true; events?.close(); root.replaceChildren();
        message('La sesión terminó o ya no tienes acceso al tablero.', true);
    }
    function connect() {
        const plan = selected() || '';
        const task = selectedTask() || '';
        const filter = `${plan}:${task}`;
        if (!window.EventSource || stopped || events && eventPlan === filter) return;
        events?.close(); realtime = false; eventPlan = filter;
        const url = new URL('/tablero', location.origin);
        url.searchParams.set('handler', 'Events');
        if (plan) url.searchParams.set('plan', plan);
        if (task) url.searchParams.set('task', task);
        events = new EventSource(url);
        events.onopen = () => { realtime = true; liveStatus(); };
        events.onerror = () => { realtime = false; liveStatus(); };
        events.addEventListener('changed', () => {
            if (refreshing) dirty = true; else refresh(selected());
        });
        events.addEventListener('expired', expire);
        liveStatus();
    }
    const now = () => serverAt + performance.now() - syncedAt;
    function synchronize() {
        serverAt = Number(root.querySelector('[data-server-now]').dataset.serverNow);
        syncedAt = performance.now();
    }
    function colors() {
        if (stopped) return;
        const instant = now();
        root.querySelectorAll('.board-card').forEach(card => {
            const due = Number(card.dataset.due), remaining = due - instant;
            const duration = due - Number(card.dataset.start);
            const color = card.dataset.delivered === 'true' ? 'delivered' : instant < Number(card.dataset.start) ? 'scheduled' :
                remaining <= 0 || remaining <= duration * 0.10 ? 'urgent' : remaining <= duration * 0.30 ? 'warning' : 'active';
            card.className = `board-card board-card--${color}`;
            card.dataset.priority = { urgent: 0, active: 1, warning: 2, scheduled: 3, delivered: 4 }[color];
            card.querySelector('.board-card__state').textContent = {
                delivered: '✓ Entregada', scheduled: 'Por activar', active: 'Activa', warning: 'Último 30% del tiempo',
                urgent: remaining <= 0 ? 'Vencida' : 'Último 10% del tiempo'
            }[color];
        });
        const grid = root.querySelector('.board-grid');
        if (grid) {
            const cards = [...grid.querySelectorAll('.board-card')];
            const ordered = [...cards].sort((a, b) => Number(a.dataset.priority) - Number(b.dataset.priority) ||
                Number(a.dataset.due) - Number(b.dataset.due) || a.dataset.taskId.localeCompare(b.dataset.taskId));
            if (ordered.some((card, index) => card !== cards[index])) grid.append(...ordered);
        }
    }
    function message(text, login = false) {
        notice.replaceChildren(document.createTextNode(text));
        if (login) {
            const link = document.createElement('a'); link.href = '/acceso'; link.textContent = ' Iniciar sesión'; notice.append(link);
        }
        notice.hidden = false;
    }
    async function refresh(plan, change = false, task = selectedTask()) {
        if (refreshing || stopped || !change && performance.now() < retryAfter) return;
        refreshing = true;
        root.setAttribute('aria-busy', 'true');
        const selector = root.querySelector('#board-plan');
        const taskSelector = root.querySelector('#board-task');
        const focused = [selector, taskSelector].includes(document.activeElement) ? document.activeElement.id : null;
        const viewport = root.querySelector('.board-viewport');
        const scroll = change ? 0 : viewport.scrollTop;
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);
        try {
            const url = new URL('/tablero', location.origin);
            url.searchParams.set('handler', 'Refresh');
            if (plan) url.searchParams.set('plan', plan);
            if (task) url.searchParams.set('task', task);
            const response = await fetch(url, { cache: 'no-store', credentials: 'same-origin', signal: controller.signal });
            if (response.redirected || response.status === 401 || response.status === 403) {
                expire(); return;
            }
            if (!response.ok) throw new Error('refresh');
            const html = await response.text();
            const template = document.createElement('template'); template.innerHTML = html;
            if (!template.content.querySelector('.board-content')) throw new Error('content');
            root.replaceChildren(template.content);
            synchronize(); colors();
            root.querySelector('.board-viewport').scrollTop = scroll;
            if (focused) root.querySelector(`#${focused}`).focus({ preventScroll: true });
            notice.hidden = true;
            retryAfter = 0;
            const actualPlan = root.querySelector('[data-plan]').dataset.plan;
            const address = new URL(location.href);
            address.searchParams.delete('handler');
            if (actualPlan) address.searchParams.set('plan', actualPlan); else address.searchParams.delete('plan');
            const actualTask = selectedTask();
            if (actualTask) address.searchParams.set('task', actualTask); else address.searchParams.delete('task');
            history.replaceState(null, '', address);
            connect(); liveStatus();
        } catch {
            retryAfter = performance.now() + 30000;
            // Keep the last confirmed plan selected when the network fails.
            if (selector) selector.value = root.querySelector('[data-plan]').dataset.plan;
            if (taskSelector) taskSelector.value = selectedTask();
            message('Sin actualización. Mostrando los últimos datos recibidos; reintentando conexión.');
        } finally {
            clearTimeout(timeout); refreshing = false; root.setAttribute('aria-busy', 'false');
            if (dirty && !stopped) { dirty = false; queueMicrotask(() => refresh(selected())); }
        }
    }
    const selected = () => root.querySelector('[data-plan]')?.dataset.plan;
    const selectedTask = () => root.querySelector('[data-task]')?.dataset.task;
    root.addEventListener('click', event => {
        const back = event.target.closest('[data-board-back]');
        if (!back || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || !document.referrer) return;
        const previous = new URL(document.referrer);
        if (previous.origin === location.origin && previous.pathname !== '/tablero' && history.length > 1) {
            event.preventDefault();
            history.back();
        }
    });
    root.addEventListener('change', event => {
        if (event.target.id === 'board-plan') {
            if (refreshing) { event.target.value = selected(); return; }
            scrollDirection = 1; refresh(event.target.value, true, '');
        }
        if (event.target.id === 'board-task') {
            if (refreshing) { event.target.value = selectedTask(); return; }
            scrollDirection = 1; refresh(selected(), true, event.target.value);
        }
    });
    root.addEventListener('submit', event => { if (event.target.matches('.board-plan')) event.preventDefault(); });
    ['wheel', 'pointerdown', 'keydown', 'touchstart'].forEach(type => root.addEventListener(type, () => {
        pauseUntil = performance.now() + 20000;
    }, { passive: true }));
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(selected()); });
    window.addEventListener('online', () => refresh(selected()));
    synchronize(); colors(); connect();
    // Poll only as a fallback; the server pushes committed changes while connected.
    setInterval(() => { if (!realtime) refresh(selected()); }, 5000);
    setInterval(() => { if (realtime) refresh(selected()); }, 60000);
    window.addEventListener('pagehide', () => events?.close());
    window.addEventListener('pageshow', event => { if (event.persisted) { events = null; connect(); refresh(selected()); } });
    setInterval(() => {
        colors();
        const content = root.querySelector('[data-day-end]');
        if (content && now() >= Number(content.dataset.dayEnd)) refresh(selected());
    }, 1000);
    // Slowly cycle through overflowing cards on unattended branch displays.
    setInterval(() => {
        const viewport = root.querySelector('.board-viewport');
        if (!viewport || stopped || document.hidden || notice.hidden === false ||
            matchMedia('(prefers-reduced-motion: reduce)').matches ||
            performance.now() < Math.max(pauseUntil, scrollWaitUntil)) return;
        const max = viewport.scrollHeight - viewport.clientHeight;
        if (max <= 1) return;
        viewport.scrollTop += scrollDirection;
        if (scrollDirection > 0 && viewport.scrollTop >= max - 1 || scrollDirection < 0 && viewport.scrollTop <= 0) {
            scrollDirection *= -1; scrollWaitUntil = performance.now() + 7000;
        }
    }, 60);
})();
