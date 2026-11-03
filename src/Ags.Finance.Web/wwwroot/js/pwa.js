// Registra o service worker mínimo (instalação como app e aviso de servidor fora do ar). Exige HTTPS confiado no aparelho.
if ('serviceWorker' in navigator && window.isSecureContext) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(() => { /* sem HTTPS confiável não há instalação; o app segue normal no navegador */ });
    });
}
