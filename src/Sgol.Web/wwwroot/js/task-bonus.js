(() => {
    'use strict';
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const format = value => value == null ? '—' : (value < 95 && Math.round(value * 100) / 100 >= 95 ? '<95' : value < 85 && Math.round(value * 100) / 100 >= 85 ? '<85' : new Intl.NumberFormat('es-MX', { maximumFractionDigits: 2 }).format(value)) + '%';
    const localTime = value => new Intl.DateTimeFormat('es-MX', { timeZone: 'America/Mexico_City', dateStyle: 'short', timeStyle: 'medium' }).format(new Date(value));
    const state = r => r.calculationState === 'NO_VIGENTE' ? 'Evaluación aún no vigente' : r.calculationState === 'INCIDENCIA' ? 'Sin porcentaje calculable' : r.calculationState === 'SIN_TAREAS_EVALUABLES' ? 'Sin tareas evaluables' : r.closed ? 'Cerrado' : 'Provisional';
    const eventLabel = kind => ({ SUBMITTED: 'Enviada a revisión', RETURNED: 'Devuelta para corrección', REJECTED: 'No satisfecha', ADMIN_COMPLETION: 'Concluida por Supervisión', APPROVED: 'Revisión confirmada', CANCELLED: 'Cancelación autorizada', EXCLUDED: 'Excluida del bono por Dirección', TRANSFERRED: 'Obligación transferida', TRANSFERRED_OVERDUE: 'Transferida para recuperación' })[kind] || 'Cambio registrado';
    function text(root, selector, value) { root.querySelectorAll(selector).forEach(e => e.textContent = value); }
    function animate(gauge, before, after) {
        if (before == null || after == null || before === after || reduced.matches) return;
        const needle = gauge.querySelector('.task-bonus__needle');
        const start = () => {
            gauge.classList.remove('task-bonus--animate'); needle.style.transform = `rotate(${before * 1.8}deg)`;
            requestAnimationFrame(() => requestAnimationFrame(() => { gauge.classList.add('task-bonus--animate'); needle.style.transform = `rotate(${after * 1.8}deg)`; }));
        };
        const observer = new IntersectionObserver(entries => { if (entries.some(e => e.isIntersecting)) { observer.disconnect(); start(); } });
        observer.observe(gauge);
    }
    function update(gauge, r) {
        const before = gauge.dataset.score === '' ? null : Number(gauge.dataset.score);
        const changed = before !== r.scorePercent;
        gauge.dataset.score = r.scorePercent ?? ''; gauge.dataset.version = r.resultVersion;
        gauge.dataset.week = r.period.startsOn;
        const date = value => new Intl.DateTimeFormat('es-MX', { timeZone: 'UTC', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value + 'T12:00:00Z'));
        text(gauge, '[data-bonus-period]', date(r.period.startsOn) + '–' + date(r.period.endsOn));
        const cutoff = new Date(r.period.startsOn + 'T12:00:00Z'); cutoff.setUTCDate(cutoff.getUTCDate() + 7);
        text(gauge, '[data-bonus-cutoff]', 'Lunes ' + date(cutoff.toISOString().slice(0, 10)) + ', 12:00');
        text(gauge, '[data-bonus-payment]', date(r.period.paymentReferenceDate));
        gauge.querySelector('[data-bonus-first-week]').hidden = r.period.startsOn !== '2026-11-02';
        const scoreLabel = r.scoreDisplay + (r.scorePercent == null ? '' : '%');
        text(gauge, '[data-bonus-score], [data-bonus-summary]', scoreLabel);
        text(gauge, '[data-bonus-retained]', r.bonusPercent == null ? '—' : r.bonusPercent + '%');
        text(gauge, '[data-bonus-state]', state(r));
        text(gauge, '[data-bonus-base]', r.evaluablePoints);
        text(gauge, '[data-bonus-deducted]', r.deductedPoints);
        text(gauge, '[data-bonus-incidences]', r.incidences.join(' '));
        gauge.querySelector('[data-bonus-incidences]').hidden = r.incidences.length === 0;
        text(gauge, '[data-bonus-at]', localTime(r.calculatedAt));
        const meter = gauge.querySelector('.task-bonus__score');
        if (r.scorePercent == null) { ['role', 'aria-valuenow', 'aria-valuemin', 'aria-valuemax'].forEach(a => meter.removeAttribute(a)); }
        else { meter.setAttribute('role', 'meter'); meter.setAttribute('aria-valuenow', r.scorePercent); meter.setAttribute('aria-valuemin', '0'); meter.setAttribute('aria-valuemax', '100'); }
        gauge.querySelector('.task-bonus__trigger').setAttribute('aria-label', `Porcentaje semanal de ${r.personName}: ${scoreLabel}. Ver información`);
        const needle = gauge.querySelector('.task-bonus__needle'); needle.setAttribute('visibility', r.scorePercent == null ? 'hidden' : 'visible');
        needle.style.transform = `rotate(${(r.scorePercent ?? 0) * 1.8}deg)`;
        animate(gauge, before, r.scorePercent);
        const person = gauge.closest('[data-bonus-person]');
        if (person) {
            const body = person.querySelector('[data-bonus-rows]');
            if (body) {
                const rows = r.details.map(d => {
                    const row = document.createElement('tr');
                    for (const [i, value] of [`${d.taskCode} · ${d.name}\n${d.day}`, d.criticality, d.points, d.deducted, `${d.reason}\n${d.submittedAt ? (d.creditKind === 'ADMIN_COMPLETION' ? 'Conclusión administrativa: ' : 'Envío: ') + localTime(d.submittedAt) : 'Sin envío acreditado'}\n${d.events.map(e => localTime(e.at) + ' · ' + eventLabel(e.kind) + (e.reason ? ' · ' + e.reason : '')).join('\n')}`].entries()) {
                        const cell = document.createElement(i === 0 ? 'th' : 'td'); cell.textContent = value; cell.style.whiteSpace = 'pre-line';
                        if (i === 0) { cell.scope = 'row'; const link = document.createElement('a'); link.href = '/validaciones?task=' + encodeURIComponent(d.assignmentId); link.textContent = value; cell.replaceChildren(link); }
                        row.append(cell);
                    }
                    return row;
                });
                body.replaceChildren(...rows);
            }
            if (r.closed) person.querySelectorAll('.bonus-exclusion').forEach(f => f.remove());
        }
        return changed;
    }
    // Board refreshes replace gauges, so closing must also handle newly rendered dialogs.
    document.addEventListener('close', e => {
        if (e.target.matches('.task-bonus__dialog')) e.target.closest('[data-task-bonus]')?.querySelector('.task-bonus__trigger')?.focus();
    }, true);
    document.addEventListener('keydown', e => {
        const dialog = e.target.closest('.task-bonus__dialog[open]');
        if (!dialog || e.key !== 'Tab') return;
        const controls = [...dialog.querySelectorAll('button, a[href], [tabindex="0"]')].filter(c => !c.disabled && c.getClientRects().length);
        const first = controls[0], last = controls.at(-1);
        if (first && (e.shiftKey && document.activeElement === first || !e.shiftKey && document.activeElement === last)) {
            e.preventDefault(); (e.shiftKey ? last : first).focus();
        }
    });
    document.addEventListener('submit', e => {
        if (e.target.method?.toLowerCase() !== 'post') return;
        document.querySelectorAll('[data-task-bonus]').forEach(g => {
            try { sessionStorage.setItem('sgol-bonus-return:' + g.dataset.person + ':' + g.dataset.week, JSON.stringify({ score: g.dataset.score, version: g.dataset.version, at: Date.now() })); } catch { }
        });
    }, true);
    document.querySelectorAll('[data-task-bonus]').forEach(g => {
        try {
            const key = 'sgol-bonus-return:' + g.dataset.person + ':' + g.dataset.week;
            const raw = sessionStorage.getItem(key); sessionStorage.removeItem(key); if (!raw) return;
            const previous = JSON.parse(raw); if (Date.now() - previous.at > 600000 || previous.score === '' || g.dataset.score === '') return;
            const before = Number(previous.score), after = Number(g.dataset.score); if (!Number.isFinite(before) || before < 0 || before > 100) return;
            animate(g, before, after);
            if (before === after) return;
            const notice = document.createElement('p'); notice.className = 'alerta alerta--info'; notice.setAttribute('role', 'status');
            notice.textContent = `Porcentaje actualizado: ${g.querySelector('[data-bonus-score]').textContent}. `;
            const link = document.createElement('a'); link.href = '#' + g.id; link.textContent = 'Ver porcentaje'; notice.append(link);
            (document.getElementById('plan-tasks') || g.closest('[data-bonus-feed]')).prepend(notice);
        } catch { }
    });
    document.querySelectorAll('[data-bonus-feed]').forEach(feed => {
        let etag = '', busy = false, stopped = false, failures = 0, timer, sequence = 0;
        let controller = new AbortController();
        async function poll() {
            clearTimeout(timer); if (stopped || busy || document.hidden) return;
            busy = true; const request = ++sequence;
            const lifetime = controller;
            const requestController = new AbortController();
            const cancel = () => requestController.abort();
            lifetime.signal.addEventListener('abort', cancel, { once: true });
            const requestTimeout = setTimeout(cancel, 10000);
            try {
                const response = await fetch(feed.dataset.bonusFeed, { credentials: 'same-origin', cache: 'no-store', headers: etag ? { 'If-None-Match': etag } : {}, signal: requestController.signal });
                if (request !== sequence) return;
                if ([401, 403].includes(response.status) || response.redirected) {
                    stopped = true; feed.querySelectorAll('[data-task-bonus], [data-bonus-rows], .bonus-exclusion').forEach(e => e.remove());
                    text(feed, '[data-bonus-status]', 'La sesión terminó o cambió el acceso. Vuelve a iniciar sesión.'); return;
                }
                if (response.status === 304) {
                    failures = 0;
                    feed.querySelectorAll('[data-task-bonus]').forEach(g => g.classList.remove('task-bonus--stale'));
                    text(feed, '[data-bonus-status]', '');
                    return;
                }
                if (!response.ok) throw new Error('No disponible');
                const results = await response.json(); if (request !== sequence) return;

                etag = response.headers.get('etag') || ''; failures = 0;
                const visiblePeople = new Set(results.map(r => r.personId));
                feed.querySelectorAll('[data-bonus-person]').forEach(p => { if (!visiblePeople.has(p.dataset.bonusPerson)) p.remove(); });
                let changed = false;
                results.forEach(r => {
                    const gauge = [...feed.querySelectorAll('[data-task-bonus]')].find(g => g.dataset.person === r.personId && (feed.hasAttribute('data-bonus-current') || g.dataset.week === r.period.startsOn));
                    if (gauge) { gauge.classList.remove('task-bonus--stale'); changed = update(gauge, r) || changed; }
                });
                text(feed, '[data-bonus-status]', changed ? 'Calificación actualizada.' : '');
            } catch (e) {
                if (request !== sequence || e.name === 'AbortError' && lifetime.signal.aborted) return;
                failures++; feed.querySelectorAll('[data-task-bonus]').forEach(g => g.classList.add('task-bonus--stale'));
                text(feed, '[data-bonus-status]', 'Dato desactualizado. Se conserva la última consulta; reintentando…');
            } finally {
                clearTimeout(requestTimeout); lifetime.signal.removeEventListener('abort', cancel);
                if (request === sequence) {
                    busy = false; if (!stopped) timer = setTimeout(poll, Math.min(30000, 5000 * 2 ** Math.min(failures, 3)));
                }
            }
        }
        timer = setTimeout(poll, 5000);
        document.addEventListener('visibilitychange', () => { if (!document.hidden) { etag = ''; poll(); } else clearTimeout(timer); });
        window.addEventListener('focus', () => { etag = ''; poll(); });
        window.addEventListener('pagehide', () => { stopped = true; sequence++; clearTimeout(timer); controller.abort(); });
        window.addEventListener('pageshow', e => { if (e.persisted) { controller = new AbortController(); stopped = false; busy = false; etag = ''; poll(); } });
    });
})();
