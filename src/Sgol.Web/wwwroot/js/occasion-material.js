(() => {
    'use strict';
    document.querySelectorAll('[data-occasion-material]').forEach(section => {
        section.querySelector('[data-copy-publication]')?.addEventListener('click', async () => {
            const text = section.querySelector('[data-publication-text]');
            const status = section.querySelector('[data-copy-status]');
            try { await navigator.clipboard.writeText(text.value); status.textContent = 'Texto copiado.'; }
            catch { text.focus(); text.select(); status.textContent = 'Selecciona Copiar en tu dispositivo para copiar el texto.'; }
        });
    });
    const form = document.querySelector('[data-occasion-form]');
    if (!form) return;
    const automatic = form.querySelector('[data-occasion-automatic]');
    const recipients = form.querySelector('[data-occasion-recipients]');
    const update = () => { recipients.disabled = automatic?.checked === true; recipients.hidden = automatic?.checked === true; };
    automatic?.addEventListener('change', update); update();
    form.addEventListener('submit', event => {
        if (!automatic?.checked && !recipients.querySelector('input:checked')) {
            event.preventDefault(); form.querySelector('[data-occasion-status]').textContent = 'Selecciona al menos una persona.'; return;
        }
        form.querySelector('button[type=submit]').disabled = true;
        form.querySelector('[data-occasion-status]').textContent = 'Verificando material y asignando tareas…';
    });
})();
