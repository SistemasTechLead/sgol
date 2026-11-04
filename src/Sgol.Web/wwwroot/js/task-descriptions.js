/* Expand catalog descriptions across the table; preserve native disclosure on mobile/no JS. */
document.addEventListener('DOMContentLoaded', () => {
    'use strict';
    const table = document.querySelector('.task-catalog-table');
    if (!table) return;
    const entries = [...table.querySelectorAll('[data-task-description]')].map((details, index) => {
        const content = details.querySelector('.markdown-content');
        const summary = details.querySelector('summary');
        const owner = details.closest('tr');
        const row = document.createElement('tr');
        row.className = 'task-catalog-description-row';
        const cell = row.insertCell();
        cell.colSpan = owner.cells.length;
        content.id = `task-description-content-${index}`;
        summary.id = `task-description-toggle-${index}`;
        summary.setAttribute('aria-controls', content.id);
        content.setAttribute('role', 'region');
        content.setAttribute('aria-labelledby', summary.id);
        return { details, content, summary, owner, row, cell };
    });
    const arrange = () => {
        const root = document.documentElement;
        const wide = root.clientWidth > 48 * parseFloat(getComputedStyle(root).fontSize);
        entries.forEach(({ details, content, summary, owner, row, cell }) => {
            const focused = document.activeElement;
            if (wide && details.open) {
                if (content.parentElement !== cell) cell.append(content);
                if (!row.isConnected) owner.after(row);
            } else {
                if (content.parentElement !== details) details.append(content);
                row.remove();
            }
            summary.setAttribute('aria-expanded', String(details.open));
            if (focused !== document.body && focused !== document.activeElement && content.contains(focused)) {
                (details.open ? focused : summary).focus({ preventScroll: true });
            }
        });
    };
    entries.forEach(({ details }) => details.addEventListener('toggle', arrange));
    let frame = 0;
    new ResizeObserver(() => {
        if (frame) return;
        frame = requestAnimationFrame(() => { frame = 0; arrange(); });
    }).observe(document.documentElement);
    arrange();
});
