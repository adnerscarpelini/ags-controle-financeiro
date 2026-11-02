document.addEventListener('keydown', event => {
    if (!event.ctrlKey || event.altKey || event.shiftKey) return;
    if (event.key.toLowerCase() === 'n') {
        const button = document.querySelector('[data-new-entry]');
        if (!button) return;
        event.preventDefault();
        button.click();
        document.getElementById('entry-merchant')?.focus();
    }
    if (event.key.toLowerCase() === 'd') {
        const button = document.querySelector('[data-duplicate-selected]');
        if (!button) return;
        event.preventDefault();
        button.click();
    }
});
