document.addEventListener('keydown', event => {
    if (!event.ctrlKey || event.altKey || event.shiftKey) return;
    if (event.key.toLowerCase() === 'n') {
        const button = document.querySelector('[data-new-entry-menu] button');
        if (!button) return;
        event.preventDefault();
        button.click();
    }
    if (event.key.toLowerCase() === 'd') {
        const button = document.querySelector('[data-duplicate-selected]');
        if (!button) return;
        event.preventDefault();
        button.click();
    }
    if (event.key.toLowerCase() === 'f') {
        const focusSearch = () => {
            const search = document.querySelector('[data-entry-search]');
            if (search) {
                search.focus();
                search.select();
            }
            return Boolean(search);
        };
        if (focusSearch()) {
            event.preventDefault();
            return;
        }
        // A busca é recolhida: o botão abre o campo, que chega do servidor logo depois.
        const toggle = document.querySelector('[data-entry-search-toggle]');
        if (!toggle) return;
        event.preventDefault();
        toggle.click();
        let attempts = 0;
        const timer = setInterval(() => {
            attempts += 1;
            if (focusSearch() || attempts > 20) clearInterval(timer);
        }, 50);
    }
});
