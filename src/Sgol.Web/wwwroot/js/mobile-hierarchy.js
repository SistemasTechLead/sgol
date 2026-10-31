document.addEventListener('DOMContentLoaded', () => {
    const root = document.documentElement;
    const entries = [...document.querySelectorAll('.section-navigation:not([data-section="tasks"]) > [data-presentation="primary"]')].map(element => {
        const home = document.createComment('primary action desktop position');
        element.before(home);
        return { element, home, mobile: false };
    });
    const restore = () => entries.forEach(entry => {
        if (entry.mobile) entry.home.after(entry.element);
        entry.mobile = false;
    });
    const arrange = () => {
        const mobile = !matchMedia('print').matches && root.getBoundingClientRect().width <= 48 * parseFloat(getComputedStyle(root).fontSize);
        entries.forEach(entry => {
            if (entry.mobile === mobile) return;
            const focused = document.activeElement === entry.element;
            if (mobile) entry.element.parentElement.append(entry.element);
            else entry.home.after(entry.element);
            entry.mobile = mobile;
            if (focused) entry.element.focus({preventScroll: true});
        });
    };
    let frame = 0;
    const schedule = () => { if (!frame) frame = requestAnimationFrame(() => { frame = 0; arrange(); }); };
    new ResizeObserver(schedule).observe(root);
    matchMedia('print').addEventListener('change', schedule);
    window.addEventListener('beforeprint', restore);
    window.addEventListener('afterprint', schedule);
    arrange();
});
