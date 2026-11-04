(() => {
    'use strict';
    window.SgolSearch = Object.freeze({
        normalize: value => (value ?? '').normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase('es-MX'),
        isStandalone: form => {
            if (!form?.matches('form')) return false;
            // Hidden context and submit/reset actions are not additional filters.
            const fields = [...form.querySelectorAll('input, select, textarea')]
                .filter(field => !field.matches('input[type="hidden"], input[type="submit"], input[type="reset"], input[type="button"]'));
            return fields.length === 1 && fields[0].matches('input[type="search"]');
        }
    });
})();
