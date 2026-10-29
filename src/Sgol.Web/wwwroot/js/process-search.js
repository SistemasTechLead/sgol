(() => {
    'use strict';
    const form = document.querySelector('[data-process-search]');
    const results = document.querySelector('[data-process-search-results]');
    const status = document.querySelector('[data-process-search-status]');
    if (!form || !results || !status) return;
    const input = form.querySelector('[name="Search"]');
    let pending = null;
    form.addEventListener('submit', async event => {
        // Searching must not navigate away from, save or discard the editor.
        event.preventDefault();
        pending?.abort();
        const request = new AbortController();
        pending = request;
        const url = new URL(form.action);
        url.search = new URLSearchParams(new FormData(form)).toString();
        results.setAttribute('aria-busy', 'true');
        status.textContent = 'Buscando tareas…';
        try {
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', signal: request.signal });
            if (!response.ok || new URL(response.url).pathname !== url.pathname)
                throw new Error(response.status === 403 || new URL(response.url).pathname === '/acceso'
                    ? 'Tu sesión no permite consultar las tareas. Conserva los cambios del editor y vuelve a iniciar sesión.'
                    : 'No se pudo buscar. Tus cambios siguen en el editor; vuelve a intentarlo.');
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            const found = page.querySelector('[data-process-search-results]');
            if (!found) throw new Error('No se pudo leer la búsqueda. Tus cambios siguen en el editor; vuelve a intentarlo.');
            if (pending !== request) return;
            results.replaceChildren(...Array.from(found.childNodes, node => document.importNode(node, true)));
            const count = results.querySelectorAll('.process-candidate').length;
            status.textContent = count ? `${count} ${count === 1 ? 'tarea mostrada' : 'tareas mostradas'}. La búsqueda conserva los cambios sin guardar.` : 'No se encontraron tareas. Prueba otro nombre o código.';
            input.focus({ preventScroll: true });
        } catch (error) {
            if (error.name !== 'AbortError' && pending === request) status.textContent = error instanceof TypeError ? 'No se pudo buscar. Tus cambios siguen en el editor; vuelve a intentarlo.' : error.message;
        } finally {
            if (pending === request) { results.removeAttribute('aria-busy'); pending = null; }
        }
    });
})();
