// Aplica o tema escolhido nas configurações sem recarregar a página. O servidor já entrega o tema salvo no HTML inicial.
window.agsTheme = {
    apply(theme) {
        if (theme === 'light' || theme === 'dark' || theme === 'auto') {
            document.documentElement.dataset.theme = theme;
        }
    },
};
