(() => {
    "use strict";
    const focus = () => {
        if (document.querySelector("[data-work-error], [data-front-error]")) return;
        const id = location.hash.slice(1);
        const target = document.getElementById(id) || (id ? document.querySelector("h1") : null);
        target?.focus();
    };
    if (document.readyState === "complete") focus();
    else window.addEventListener("load", focus, { once: true });
})();
