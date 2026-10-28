(() => {
    const root = document.getElementById('live-board');
    const notice = document.getElementById('board-connection');
    if (!root || !notice) return;
    let serverAt = 0, syncedAt = 0, refreshing = false, stopped = false;
    let pauseUntil = 0, scrollDirection = 1, scrollWaitUntil = performance.now() + 7000;
    let retryAfter = 0;
    let events = null, eventPlan = null, realtime = false, dirty = false;
    let progressStates = [];
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
    function animateProgress(previous) {
        root.querySelectorAll('[data-progress-key]').forEach(progress => {
            const fill = progress?.querySelector('[data-board-progress-fill]');
            const next = Number(progress?.dataset.progressPercent);
            const old = previous.get(progress.dataset.progressKey);
            if (!fill?.animate || !old || !Number.isFinite(old.percent) || old.percent === next ||
                old.period !== progress.dataset.progressPeriod ||
                window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
            fill.animate([
                { width: `${old.percent}%` },
                { width: `${next}%` }
            ], { duration: 650, easing: 'cubic-bezier(.22, 1, .36, 1)' });
        });
    }
    function synchronize() {
        serverAt = Number(root.querySelector('[data-server-now]').dataset.serverNow);
        syncedAt = performance.now();
        progressStates = [...root.querySelectorAll('[data-pace-schedule]')].map(progress => ({
            progress, schedule: JSON.parse(progress.dataset.paceSchedule || '[]')
        }));
    }
    function pace(instant) {
        for (const {progress, schedule} of progressStates) {
            let expected = 0;
            const delivered = Number(progress.dataset.progressDelivered);
            for (const [due, count] of schedule) {
                if (due > instant) break;
                expected += count;
            }
            const color = expected === 0 ? 'neutral' : delivered * 100 >= expected * Number(progress.dataset.paceGreen) ? 'green' :
                delivered * 100 >= expected * Number(progress.dataset.paceYellow) ? 'yellow' : 'red';
            progress.className = `board-progress board-progress--${color}`;
            if (Number(progress.dataset.paceExpected) === expected && Number(progress.dataset.paceDelivered) === delivered) continue;
            progress.dataset.paceExpected = expected;
            progress.dataset.paceDelivered = delivered;
            const total = Number(progress.dataset.progressTotal);
            const position = `${total === 0 ? 0 : expected * 100 / total}%`;
            const marker = progress.querySelector('[data-pace-marker]');
            marker.setAttribute('x1', position); marker.setAttribute('x2', position);
            progress.querySelector('[data-pace-detail]').textContent = `${delivered}/${expected} para esta hora`;
            progress.querySelector('[data-pace-reference]').title = `Referencia: ${expected} de ${total} tareas para esta hora`;
            progress.querySelector('progress').setAttribute('aria-label',
                `${progress.getAttribute('aria-label')}: ${delivered} de ${total} tareas entregadas, ${progress.dataset.progressPercent} por ciento; ${delivered} de ${expected} para esta hora`);
        }
    }
    function colors() {
        if (document.hidden) return;
        if (stopped) return;
        const instant = now();
        const stateFilter = root.querySelector('[data-state]')?.dataset.state;
        pace(instant);
        root.querySelectorAll('.board-card').forEach(card => {
            const due = Number(card.dataset.due), remaining = due - instant;
            const duration = due - Number(card.dataset.start);
            const color = card.dataset.delivered === 'true' ? 'delivered' : instant < Number(card.dataset.start) ? 'scheduled' :
                remaining <= 0 || remaining <= duration * 0.10 ? 'urgent' : remaining <= duration * 0.30 ? 'warning' : 'active';
            card.className = `board-card board-card--${color}`;
            const state = color === 'urgent' && remaining <= 0 ? 'overdue' : color;
            card.hidden = Boolean(stateFilter && stateFilter !== state);
            card.dataset.priority = { urgent: 0, active: 1, warning: 2, scheduled: 3, delivered: 4 }[color];
            card.querySelector('.board-card__state').textContent = {
                delivered: '✓ Entregada', scheduled: 'Por activar', active: 'Activa', warning: 'Último 30% del tiempo',
                urgent: remaining <= 0 ? 'Vencida' : 'Último 10% del tiempo'
            }[color];
        });
        const grid = root.querySelector('.board-grid');
        const empty = root.querySelector('[data-board-empty]');
        if (empty) empty.hidden = Boolean(root.querySelector('.board-card:not([hidden])'));
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
        if (document.hidden || refreshing || stopped || !change && performance.now() < retryAfter) return;
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
            const previousProgress = new Map([...root.querySelectorAll('[data-progress-key]')].map(progress => [
                progress.dataset.progressKey, { percent: Number(progress.dataset.progressPercent), period: progress.dataset.progressPeriod }
            ]));
            root.replaceChildren(template.content);
            animateProgress(previousProgress);
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
    root.addEventListener('change', event => {
        if (event.target.id === 'board-plan') {
            if (refreshing) { event.target.value = selected(); return; }
            scrollDirection = 1; refresh(event.target.value, true, selectedTask()?.startsWith('state:') ? selectedTask() : '');
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
        if (document.hidden) return;
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
