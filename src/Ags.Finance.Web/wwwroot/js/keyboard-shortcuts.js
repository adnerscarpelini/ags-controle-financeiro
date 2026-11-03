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
        const search = document.querySelector('[data-entry-search]');
        if (!search) return;
        event.preventDefault();
        search.focus();
        search.select();
    }
});
