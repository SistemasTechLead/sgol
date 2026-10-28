(() => {
    'use strict';
    const storageKey = 'sgol.appearance.theme';
    const root = document.documentElement;
    // Restore navigation width before the first paint and across page navigation.
    try {
        const width = Number(localStorage.getItem('sgol.appearance.sidebarWidth'));
        if (Number.isFinite(width) && width >= 72 && width <= 224) {
            root.style.setProperty('--ancho-navegacion', `${width}px`);
            root.dataset.sidebarCompact = String(width < 180);
        }
    } catch { /* Storage is optional. */ }
    const layoutKey = 'sgol.appearance.layout';
    const normalizeLayout = value => ['desktop', 'mobile'].includes(value) ? value : 'auto';
    const readLayout = () => {
        try { return normalizeLayout(localStorage.getItem(layoutKey)); }
        catch { return 'auto'; }
    };
    const applyLayout = layout => {
        root.dataset.layout = layout;
        document.querySelectorAll('[data-layout-choice]').forEach(choice => {
            choice.checked = choice.value === layout;
        });
        // A desktop viewport lets phones show the full desktop composition.
        const viewport = document.querySelector('meta[name="viewport"]');
        if (viewport) viewport.content = layout === 'desktop'
            ? 'width=1280' : 'width=device-width, initial-scale=1';
        const navigation = document.querySelector('#navegacion-movil');
        if (layout !== 'mobile' && navigation?.open) navigation.close();
    };
    const normalize = value => ['dark', 'special'].includes(value) ? value : 'light';
    const read = () => {
        // Dedicated displays keep their appearance across private browser sessions.
        if (root.dataset.screenTheme === 'dark') return 'dark';
        try { return normalize(localStorage.getItem(storageKey)); }
        catch { return 'light'; }
    };
    const apply = theme => {
        root.dataset.theme = theme;
        document.querySelectorAll('[data-theme-choice]').forEach(choice => {
            choice.checked = choice.value === theme;
        });
    };
    // Runs in the head before styles paint, including on the sign-in page.
    apply(read());
    applyLayout(readLayout());
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-layout-choice]').forEach(choice => {
            choice.disabled = false;
            choice.checked = choice.value === root.dataset.layout;
            choice.addEventListener('change', () => {
                if (!choice.checked) return;
                const layout = normalizeLayout(choice.value);
                applyLayout(layout);
                let saved = true;
                try { localStorage.setItem(layoutKey, layout); }
                catch { saved = false; }
                const status = document.querySelector('[data-layout-status]');
                const label = { desktop: 'Escritorio', mobile: 'Móvil', auto: 'Automática' }[layout];
                if (status) status.textContent = `Vista ${label} activada. ` +
                    (saved ? 'Se conservará en este navegador, incluso al volver a iniciar sesión.' : 'El navegador no permitió guardar la preferencia.');
            });
        });
        document.querySelectorAll('[data-theme-choice]').forEach(choice => {
            choice.disabled = false;
            choice.checked = choice.value === root.dataset.theme;
            choice.addEventListener('change', () => {
                if (!choice.checked) return;
                const theme = normalize(choice.value);
                apply(theme);
                let saved = true;
                try { localStorage.setItem(storageKey, theme); }
                catch { saved = false; }
                const status = document.querySelector('[data-theme-status]');
                const label = { light: 'Modo claro activado.', dark: 'Modo oscuro activado.', special: 'Apariencia especial activada.' }[theme];
                if (status) status.textContent = `${label} ` +
                    (saved ? 'Preferencia guardada en este navegador.' : 'El navegador no permitió guardar la preferencia.');
            });
        });
    });
    window.addEventListener('storage', event => {
        if (event.key === layoutKey || event.key === null) {
            applyLayout(readLayout());
            const status = document.querySelector('[data-layout-status]');
            if (status) status.textContent = 'La vista se actualizó desde otra pestaña.';
        }
        if (event.key === storageKey || event.key === null) {
            apply(read());
            const status = document.querySelector('[data-theme-status]');
            if (status) status.textContent = 'La apariencia se actualizó desde otra pestaña.';
        }
    });
})();
