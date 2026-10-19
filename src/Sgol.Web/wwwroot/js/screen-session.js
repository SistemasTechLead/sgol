(() => {
    const source = document.currentScript;
    const expires = Number(source?.dataset.expires) * 1000;
    if (!Number.isFinite(expires)) return;
    // Recover before absolute expiry; no permanent credential is exposed to Edge.
    const renew = () => { if (Date.now() >= expires - 120000) location.replace('/pantalla'); };
    setInterval(renew, 15000);
    window.addEventListener('online', renew);
    renew();
})();
