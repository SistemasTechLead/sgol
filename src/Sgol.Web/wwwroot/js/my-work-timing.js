(() => {
    "use strict";
    const root = document.querySelector('[data-work-now]');
    if (!root) return;
    const serverAt = Number(root.dataset.workNow), syncedAt = performance.now();
    const priorities = { urgent: 0, active: 1, warning: 2, scheduled: 3, delivered: 4 };
    function update() {
        const now = serverAt + performance.now() - syncedAt;
        root.querySelectorAll('[data-work-task]').forEach(row => {
            if (row.dataset.taskStatus === 'reserved') { row.dataset.priority = 3; return; }
            if (row.dataset.taskStatus === 'rejected' || row.dataset.taskStatus === 'reassigned') {
                row.dataset.priority = row.dataset.taskStatus === 'rejected' ? 0 : row.classList.contains('my-work-color--delivered') ? 4 : 3;
                return;
            }
            const remaining = Number(row.dataset.due) - now;
            const duration = Number(row.dataset.due) - Number(row.dataset.start);
            const color = row.dataset.delivered === 'true' ? 'delivered' : now < Number(row.dataset.start) ? 'scheduled' :
                remaining <= 0 || remaining <= duration * 0.10 ? 'urgent' : remaining <= duration * 0.30 ? 'warning' : 'active';
            for (const state of Object.keys(priorities)) row.classList.toggle(`my-work-color--${state}`, state === color);
            row.dataset.priority = priorities[color];
            const timing = { delivered: 'Entregada', scheduled: 'Por activar', active: 'Activa', warning: 'Último 30% del tiempo',
                urgent: remaining <= 0 ? 'Vencida' : 'Último 10% del tiempo' }[color];
            row.querySelectorAll('[data-work-timing]').forEach(label => { label.textContent = timing; });
            if (row.dataset.taskStatus === 'pending') row.querySelectorAll('.my-work-state').forEach(label => { label.textContent = timing; });
        });
        root.querySelectorAll('[data-work-task-list]').forEach(list => {
            const rows = [...list.querySelectorAll('[data-work-task]')];
            const ordered = [...rows].sort((a, b) => Number(a.dataset.priority) - Number(b.dataset.priority) ||
                Number(a.dataset.due) - Number(b.dataset.due) || a.dataset.workTask.localeCompare(b.dataset.workTask));
            if (ordered.some((row, index) => row !== rows[index])) list.append(...ordered);
        });
    }
    update();
    setInterval(update, 1000);
})();
