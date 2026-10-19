(() => {
    "use strict";
    const root = document.querySelector('[data-work-now]');
    if (!root) return;
    const serverAt = Number(root.dataset.workNow), syncedAt = performance.now();
    const dayStart = Number(root.dataset.workDayStart), dayEnd = Number(root.dataset.workDayEnd);
    const priorities = { urgent: 0, active: 1, warning: 2, scheduled: 3, delivered: 4 };
    function update() {
        const now = serverAt + performance.now() - syncedAt;
        root.querySelectorAll('[data-work-task]').forEach(row => {
            if (row.dataset.taskStatus === 'rejected' || row.dataset.taskStatus === 'reassigned') return;
            const remaining = Number(row.dataset.due) - now;
            const duration = Number(row.dataset.due) - Number(row.dataset.start);
            const color = row.dataset.delivered === 'true' ? 'delivered' : now < Number(row.dataset.start) ? 'scheduled' :
                remaining <= 0 || remaining <= duration * 0.10 ? 'urgent' : remaining <= duration * 0.30 ? 'warning' : 'active';
            for (const state of Object.keys(priorities)) row.classList.toggle(`my-work-color--${state}`, state === color);
            const due = Number(row.dataset.due), start = Number(row.dataset.start);
            row.dataset.priority = row.dataset.delivered === 'true' || row.dataset.taskStatus !== 'pending' ? 3 :
                due >= dayStart && due < dayEnd ? 0 : start <= now ? 1 :
                start >= dayStart && start < dayEnd ? 2 : 4;
            const timing = { delivered: 'Entregada', scheduled: 'Por activar', active: 'Activa', warning: 'Último 30% del tiempo',
                urgent: remaining <= 0 ? 'Vencida' : 'Último 10% del tiempo' }[color];
            row.querySelectorAll('[data-work-timing]').forEach(label => { label.textContent = timing; });
            if (row.dataset.taskStatus === 'pending') row.querySelectorAll('.my-work-state').forEach(label => { label.textContent = timing; });
        });
        root.querySelectorAll('[data-work-day-header="true"]').forEach(header => {
            const rows = [...header.parentElement.querySelectorAll('[data-work-day="true"]')];
            const ordered = [...rows].sort((a, b) => Number(a.dataset.priority) - Number(b.dataset.priority) ||
                Number(a.dataset.due) - Number(b.dataset.due) || a.dataset.workTask.localeCompare(b.dataset.workTask));
            if (ordered.some((row, index) => row !== rows[index])) header.after(...ordered);
        });
    }
    update();
    setInterval(update, 1000);
})();
