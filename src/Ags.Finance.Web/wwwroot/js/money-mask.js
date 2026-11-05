// Formata os campos de valor em reais enquanto se digita, com os dígitos entrando pela direita: 123456 vira 1.234,56.
// Roda antes do @oninput do Blazor, que recebe o texto já formatado, sem esperar o servidor a cada dígito.
(function () {
    "use strict";

    var MAX_DIGITS = 15;
    var AUTOFOCUS_GUARD_MS = 500;
    var lastInput = 0;
    var autofocused = null;
    var autofocusedAt = 0;

    function moneyField(el) {
        return el instanceof HTMLInputElement ? el.closest(".money-field") : null;
    }

    function format(digits, negative) {
        var trimmed = digits.replace(/^0+/, "").slice(0, MAX_DIGITS);
        var padded = trimmed.padStart(3, "0");
        var whole = padded.slice(0, -2).replace(/\B(?=(\d{3})+(?!\d))/g, ".");
        return (negative && trimmed.length > 0 ? "-" : "") + whole + "," + padded.slice(-2);
    }

    // Um número ímpar de sinais de menos deixa o valor negativo: digitar o sinal de novo volta a positivo.
    function apply(input, field) {
        var minusSigns = (input.value.match(/-/g) || []).length;
        var negative = field.hasAttribute("data-money-allow-negative") && minusSigns % 2 === 1;
        input.value = format(input.value.replace(/\D/g, ""), negative);
        input.setSelectionRange(input.value.length, input.value.length);
    }

    // O valor colado traz os próprios centavos: 1.234,5 são R$ 1.234,50, e 150 são R$ 150,00.
    function pastedValue(text) {
        var value = text.trim();
        var parts = value.match(/^(.*)[.,](\d{1,2})$/);
        var whole = parts ? parts[1] : value;
        var cents = parts ? parts[2].padEnd(2, "0") : "00";
        return (value.indexOf("-") >= 0 ? "-" : "") + whole.replace(/\D/g, "") + cents;
    }

    function caretToEnd(input) {
        if (input.selectionStart === input.selectionEnd) { input.setSelectionRange(input.value.length, input.value.length); }
    }

    document.addEventListener("input", function (event) {
        var field = moneyField(event.target);
        if (field) {
            lastInput = Date.now();
            apply(event.target, field);
        }
    }, true);

    // Os dígitos entram sempre pelo fim: clicar ou andar com as setas não leva a digitação para o meio do valor.
    document.addEventListener("click", function (event) {
        if (moneyField(event.target)) { caretToEnd(event.target); }
    }, true);

    document.addEventListener("keydown", function (event) {
        if (moneyField(event.target) && (event.key.length === 1 || event.key === "Backspace" || event.key === "Delete") && !event.ctrlKey && !event.metaKey) { caretToEnd(event.target); }
    }, true);

    document.addEventListener("paste", function (event) {
        var input = event.target;
        if (!moneyField(input) || input.disabled || input.readOnly || !event.clipboardData) { return; }
        event.preventDefault();
        input.value = pastedValue(event.clipboardData.getData("text"));
        input.dispatchEvent(new Event("input", { bubbles: true }));
    }, true);

    // Ao abrir, a janela da biblioteca põe o foco num elemento próprio, e o menu que a abriu devolve o foco ao botão dele. Os dois tiram o
    // foco do campo em AutoFocus: ele volta uma vez pela janela e, por um instante, sempre que o foco sair para fora dela.
    document.addEventListener("focusin", function (event) {
        var target = event.target;
        if (!(target instanceof HTMLElement)) { return; }
        var trap = target.parentElement;
        var input = trap && trap.classList.contains("mud-focus-trap")
            ? trap.querySelector(".money-field[data-money-autofocus] input:not([data-money-focused])")
            : null;
        if (input) {
            input.setAttribute("data-money-focused", "");
            autofocused = input;
            autofocusedAt = Date.now();
            input.focus();
            return;
        }

        if (autofocused && Date.now() - autofocusedAt < AUTOFOCUS_GUARD_MS && document.contains(autofocused) && !autofocused.closest(".mud-focus-trap").contains(target)) {
            autofocused.focus();
        }
    });

    // Ao entrar no campo o valor fica selecionado, e digitar o substitui. O atraso evita que o clique desfaça a seleção.
    document.addEventListener("focusin", function (event) {
        var input = event.target;
        if (!moneyField(input) || input.disabled) { return; }
        setTimeout(function () {
            // Quem já está digitando não perde o valor: o próximo dígito substituiria a seleção.
            if (document.activeElement === input && Date.now() - lastInput > 1000) { input.select(); }
        }, 0);
    });
})();
