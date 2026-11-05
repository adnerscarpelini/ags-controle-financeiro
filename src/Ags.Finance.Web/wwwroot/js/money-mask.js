// Formata os campos de valor em reais enquanto se digita, com os dígitos entrando pela direita: 123456 vira 1.234,56.
// Roda antes do @oninput do Blazor, que recebe o texto já formatado, sem esperar o servidor a cada dígito.
(function () {
    "use strict";

    var MAX_DIGITS = 15;

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

    document.addEventListener("input", function (event) {
        var field = moneyField(event.target);
        if (field) { apply(event.target, field); }
    }, true);

    document.addEventListener("paste", function (event) {
        var input = event.target;
        if (!moneyField(input) || input.disabled || input.readOnly || !event.clipboardData) { return; }
        event.preventDefault();
        input.value = pastedValue(event.clipboardData.getData("text"));
        input.dispatchEvent(new Event("input", { bubbles: true }));
    }, true);

    // Ao entrar no campo o valor fica selecionado, e digitar o substitui. O atraso evita que o clique desfaça a seleção.
    document.addEventListener("focusin", function (event) {
        var input = event.target;
        if (!moneyField(input) || input.disabled) { return; }
        setTimeout(function () {
            if (document.activeElement === input) { input.select(); }
        }, 0);
    });
})();
