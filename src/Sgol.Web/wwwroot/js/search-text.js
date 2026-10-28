(() => {
    'use strict';
    window.SgolSearch = Object.freeze({
        normalize: value => (value ?? '').normalize('NFD').replace(/\p{M}/gu, '').toLocaleLowerCase('es-MX')
    });
})();
