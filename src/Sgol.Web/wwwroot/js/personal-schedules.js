(() => {
    document.getElementById('people-error')?.focus();
    document.querySelectorAll('[data-schedule-day]').forEach(day => {
        const mode = day.querySelector('[data-day-mode]');
        const area = day.querySelector('[data-day-intervals]');
        const rows = [...day.querySelectorAll('[data-interval]')];
        rows.forEach((row, i) => { row.hidden = i > 0 && ![...row.querySelectorAll('input[type="time"]')].some(x => x.value); });
        const update = () => {
            area.hidden = mode.value !== 'WORK';
            rows.forEach(row => row.querySelectorAll('input').forEach(input => { input.disabled = area.hidden || row.hidden; }));
            day.querySelector('[data-add-interval]').disabled = rows.every(row => !row.hidden);
        };
        rows.forEach(row => row.querySelector('[data-remove-interval]').addEventListener('click', () => {
            row.querySelectorAll('input').forEach(input => input.value = ''); row.hidden = true; update(); day.querySelector('[data-add-interval]').focus();
        }));
        day.querySelector('[data-add-interval]').addEventListener('click', () => {
            const row = rows.find(row => row.hidden); if (row) { row.hidden = false; update(); row.querySelector('input').focus(); }
        });
        mode.addEventListener('change', update); update();
    });
})();
