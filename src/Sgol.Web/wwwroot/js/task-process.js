(() => {
    const select = document.querySelector('[data-task-process]');
    if (!select) return;
    const update = () => {
        const general = select.value === select.dataset.generalId;
        document.querySelector('[data-independent-calendar]').hidden = !general;
        const help = document.querySelector('[data-process-help]');
        help.hidden = general;
        help.querySelector('a').href = `/tareas/procesos?id=${encodeURIComponent(select.value)}`;
    };
    select.addEventListener('change', update); update();
})();
