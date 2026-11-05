// Substitui as partes abertas dos controles nativos (lista do select, calendário, seletor de mês, sugestões,
// balões de validação e dicas de title) por versões com os tokens de design (issue 112). Os elementos e o @bind do Blazor
// continuam os mesmos: ao escolher, o valor volta ao campo e os eventos input e change são disparados.
(function () {
    "use strict";

    var MONTHS = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];
    var SHORT_MONTHS = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];
    var WEEK_DAYS = ["D", "S", "T", "Q", "Q", "S", "S"];

    var popup = null;
    var anchor = null;
    var items = [];
    var active = -1;
    var typed = "";
    var typedAt = 0;
    var touchMoved = false;
    var lastTouch = 0;
    var tip = null;
    var tipTimer = 0;
    var datalistObserver = null;

    function isDateField(el) { return el instanceof HTMLInputElement && (el.type === "date" || el.type === "month"); }
    function isSelect(el) { return el instanceof HTMLSelectElement && !el.multiple; }
    function isEditable(el) { return !el.disabled && !el.readOnly; }
    function pad(n) { return String(n).padStart(2, "0"); }
    function isoDay(y, m, d) { return y + "-" + pad(m + 1) + "-" + pad(d); }
    function isoMonth(y, m) { return y + "-" + pad(m + 1); }

    function create(tag, className, text) {
        var el = document.createElement(tag);
        if (className) { el.className = className; }
        if (text !== undefined) { el.textContent = text; }
        return el;
    }

    function chevron(path) {
        var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svg.setAttribute("viewBox", "0 0 24 24");
        svg.setAttribute("aria-hidden", "true");
        svg.setAttribute("class", "picker-icon");
        var p = document.createElementNS("http://www.w3.org/2000/svg", "path");
        p.setAttribute("d", path);
        svg.appendChild(p);
        return svg;
    }

    function setValue(el, value) {
        el.value = value;
        el.dispatchEvent(new Event("input", { bubbles: true }));
        el.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function show(el, target, className) {
        close();
        popup = el;
        anchor = target;
        el.classList.add("picker");
        if (className) { el.classList.add(className); }
        el.setAttribute("popover", "manual");
        // O mouse dentro da lista não tira o foco do campo.
        el.addEventListener("pointerdown", function (e) { e.preventDefault(); });
        el.addEventListener("mousedown", function (e) { e.preventDefault(); });
        document.body.appendChild(el);
        el.showPopover();
        place(el, target);
    }

    function place(el, target) {
        var rect = target.getBoundingClientRect();
        var gap = 4;
        var margin = 8;
        el.style.minWidth = el.classList.contains("picker-list") ? rect.width + "px" : "";
        var width = el.offsetWidth;
        var height = el.offsetHeight;
        var left = Math.max(margin, Math.min(rect.left, window.innerWidth - width - margin));
        var below = window.innerHeight - rect.bottom - gap - margin;
        var top = below >= height || below >= rect.top - gap - margin ? rect.bottom + gap : rect.top - height - gap;
        el.style.left = left + "px";
        el.style.top = Math.max(margin, top) + "px";
    }

    function close() {
        if (datalistObserver) { datalistObserver.disconnect(); datalistObserver = null; }
        if (popup) { popup.remove(); }
        popup = null;
        anchor = null;
        items = [];
        active = -1;
    }

    function setActive(index) {
        if (items.length === 0) { return; }
        if (active >= 0 && items[active]) { items[active].classList.remove("is-active"); }
        active = (index + items.length) % items.length;
        items[active].classList.add("is-active");
        items[active].scrollIntoView({ block: "nearest" });
    }

    function addOption(list, label, selected, disabled, choose) {
        var option = create("button", "picker-option");
        option.type = "button";
        option.setAttribute("role", "option");
        option.setAttribute("aria-selected", selected ? "true" : "false");
        option.tabIndex = -1;
        option.disabled = disabled;
        option.appendChild(create("span", "picker-option-text", label));
        if (selected) { option.appendChild(chevron("M20 6 9 17l-5-5")); }
        option.addEventListener("click", choose);
        list.appendChild(option);
        if (!disabled) { items.push(option); }
        return option;
    }

    // Lista do select
    function openSelect(select) {
        var list = create("div", "picker-list");
        list.setAttribute("role", "listbox");
        show(list, select);
        var start = -1;
        function addFrom(option) {
            if (option.hidden) { return; }
            var option_ = addOption(list, option.textContent.trim(), option.selected, option.disabled, function () {
                var changed = select.value !== option.value;
                var target = select;
                close();
                target.focus();
                if (changed) { setValue(target, option.value); }
            });
            if (option.selected && !option.disabled) { start = items.indexOf(option_); }
        }
        Array.prototype.forEach.call(select.children, function (child) {
            if (child instanceof HTMLOptGroupElement) {
                list.appendChild(create("div", "picker-group", child.label));
                Array.prototype.forEach.call(child.children, addFrom);
            } else if (child instanceof HTMLOptionElement) {
                addFrom(child);
            }
        });
        place(list, select);
        setActive(start >= 0 ? start : 0);
    }

    // Calendário
    function openDate(input) {
        var parts = input.value.split("-").map(Number);
        var now = new Date();
        var state = input.value ? { y: parts[0], m: parts[1] - 1 } : { y: now.getFullYear(), m: now.getMonth() };
        var box = create("div", "picker-box");
        box.setAttribute("role", "dialog");
        box.setAttribute("aria-label", "Escolher data");
        function inRange(value) { return (!input.min || value >= input.min) && (!input.max || value <= input.max); }
        function draw() {
            box.textContent = "";
            box.appendChild(header(MONTHS[state.m] + " de " + state.y, function (delta) {
                var d = new Date(state.y, state.m + delta, 1);
                state.y = d.getFullYear();
                state.m = d.getMonth();
                draw();
            }, "mês"));
            var grid = create("div", "picker-days");
            WEEK_DAYS.forEach(function (d) { grid.appendChild(create("span", "picker-weekday", d)); });
            var first = new Date(state.y, state.m, 1).getDay();
            for (var i = 0; i < first; i++) { grid.appendChild(create("span")); }
            var days = new Date(state.y, state.m + 1, 0).getDate();
            var today = isoDay(now.getFullYear(), now.getMonth(), now.getDate());
            for (var day = 1; day <= days; day++) {
                grid.appendChild(cell(String(day), isoDay(state.y, state.m, day), input, today, inRange));
            }
            box.appendChild(grid);
            box.appendChild(footer(input, today, "Hoje", inRange));
        }
        draw();
        show(box, input);
    }

    // Seletor de mês
    function openMonth(input) {
        var now = new Date();
        var state = { y: input.value ? Number(input.value.split("-")[0]) : now.getFullYear() };
        var box = create("div", "picker-box");
        box.setAttribute("role", "dialog");
        box.setAttribute("aria-label", "Escolher mês");
        function inRange(value) { return (!input.min || value >= input.min) && (!input.max || value <= input.max); }
        function draw() {
            box.textContent = "";
            box.appendChild(header(String(state.y), function (delta) { state.y += delta; draw(); }, "ano"));
            var grid = create("div", "picker-months");
            var current = isoMonth(now.getFullYear(), now.getMonth());
            SHORT_MONTHS.forEach(function (label, index) {
                grid.appendChild(cell(label, isoMonth(state.y, index), input, current, inRange));
            });
            box.appendChild(grid);
            box.appendChild(footer(input, current, "Este mês", inRange));
        }
        draw();
        show(box, input);
    }

    function header(title, move, unit) {
        var head = create("div", "picker-head");
        head.appendChild(create("strong", null, title));
        var nav = create("div", "picker-nav");
        [[-1, "M15 18l-6-6 6-6", " anterior"], [1, "M9 18l6-6-6-6", " seguinte"]].forEach(function (item) {
            var button = create("button", "picker-move");
            button.type = "button";
            button.tabIndex = -1;
            button.setAttribute("aria-label", unit + item[2]);
            button.appendChild(chevron(item[1]));
            button.addEventListener("click", function () { move(item[0]); });
            nav.appendChild(button);
        });
        head.appendChild(nav);
        return head;
    }

    function cell(label, value, input, today, inRange) {
        var button = create("button", "picker-cell", label);
        button.type = "button";
        button.tabIndex = -1;
        button.disabled = !inRange(value);
        if (value === input.value) { button.classList.add("is-selected"); }
        if (value === today) { button.classList.add("is-today"); }
        button.addEventListener("click", function () { pick(input, value); });
        return button;
    }

    function footer(input, today, todayLabel, inRange) {
        var foot = create("div", "picker-foot");
        var clear = create("button", "picker-link", "Limpar");
        clear.type = "button";
        clear.tabIndex = -1;
        clear.disabled = input.required;
        clear.addEventListener("click", function () { pick(input, ""); });
        var now = create("button", "picker-link", todayLabel);
        now.type = "button";
        now.tabIndex = -1;
        now.disabled = !inRange(today);
        now.addEventListener("click", function () { pick(input, today); });
        foot.appendChild(clear);
        foot.appendChild(now);
        return foot;
    }

    function pick(input, value) {
        close();
        input.focus();
        if (input.value !== value) { setValue(input, value); }
    }

    function openField(el) {
        if (isSelect(el)) { openSelect(el); }
        else if (el.type === "date") { openDate(el); }
        else { openMonth(el); }
    }

    function toggle(el) {
        if (anchor === el) { close(); return; }
        if (document.activeElement !== el) { el.focus(); }
        openField(el);
    }

    function fieldOf(target) {
        if (isSelect(target) || isDateField(target)) { return isEditable(target) ? target : null; }
        return null;
    }

    // Sugestões de um <input list>: o atributo sai para o navegador não abrir a lista dele.
    function adoptDatalist(input) {
        var id = input.getAttribute("list");
        if (id) {
            input.dataset.suggestions = id;
            input.removeAttribute("list");
        }
    }

    function drawSuggestions(input) {
        var source = document.getElementById(input.dataset.suggestions || "");
        if (!source) { return; }
        var term = input.value.trim().toLocaleLowerCase("pt-BR");
        var values = Array.prototype.map.call(source.options, function (o) { return o.value; }).filter(function (v) {
            return term.length > 0 && v.toLocaleLowerCase("pt-BR").indexOf(term) >= 0 && v.toLocaleLowerCase("pt-BR") !== term;
        });
        if (values.length === 0) {
            if (anchor === input) { close(); }
        } else {
            var list = create("div", "picker-list");
            list.setAttribute("role", "listbox");
            show(list, input);
            values.forEach(function (value) {
                addOption(list, value, false, false, function () {
                    close();
                    input.focus();
                    setValue(input, value);
                });
            });
            place(list, input);
        }
        if (!datalistObserver) {
            datalistObserver = new MutationObserver(function () {
                if (document.activeElement === input) { drawSuggestions(input); }
            });
        }
        datalistObserver.disconnect();
        datalistObserver.observe(source, { childList: true, subtree: true, attributes: true });
    }

    // Dica própria para validação e title
    function showTip(target, text, duration) {
        hideTip();
        tip = create("div", "picker-tip", text);
        tip.setAttribute("role", "tooltip");
        tip.setAttribute("popover", "manual");
        document.body.appendChild(tip);
        tip.showPopover();
        var rect = target.getBoundingClientRect();
        var left = Math.max(8, Math.min(rect.left, window.innerWidth - tip.offsetWidth - 8));
        var top = rect.bottom + 4 + tip.offsetHeight > window.innerHeight ? rect.top - tip.offsetHeight - 4 : rect.bottom + 4;
        tip.style.left = left + "px";
        tip.style.top = top + "px";
        if (duration) { tipTimer = setTimeout(hideTip, duration); }
    }

    function hideTip() {
        clearTimeout(tipTimer);
        if (tip) { tip.remove(); }
        tip = null;
    }

    // Abertura: bloqueia a lista do sistema antes que ela apareça.
    document.addEventListener("mousedown", function (e) {
        if (Date.now() - lastTouch < 800) { return; }
        var field = fieldOf(e.target);
        if (field && isSelect(field)) {
            e.preventDefault();
            toggle(field);
        } else if (popup && !popup.contains(e.target) && e.target !== anchor) {
            close();
        }
    }, true);

    document.addEventListener("click", function (e) {
        var field = fieldOf(e.target);
        if (field && isDateField(field)) {
            e.preventDefault();
            if (Date.now() - lastTouch < 800) { return; }
            if (anchor !== field) { openField(field); }
        }
    }, true);

    document.addEventListener("touchstart", function () { touchMoved = false; }, { capture: true, passive: true });
    document.addEventListener("touchmove", function () { touchMoved = true; }, { capture: true, passive: true });
    document.addEventListener("touchend", function (e) {
        lastTouch = Date.now();
        if (touchMoved) { return; }
        var field = fieldOf(e.target);
        if (field) {
            e.preventDefault();
            toggle(field);
        } else if (popup && !popup.contains(e.target) && e.target !== anchor) {
            close();
        }
    }, { capture: true, passive: false });

    document.addEventListener("keydown", function (e) {
        var target = e.target;
        var field = fieldOf(target);
        if (popup && target === anchor) {
            if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); close(); return; }
            if (e.key === "Tab") { close(); return; }
            if (items.length > 0) {
                if (e.key === "ArrowDown") { e.preventDefault(); e.stopPropagation(); setActive(active + 1); return; }
                if (e.key === "ArrowUp") { e.preventDefault(); e.stopPropagation(); setActive(active - 1); return; }
                if ((e.key === "Enter" || (e.key === " " && isSelect(target))) && active >= 0) {
                    e.preventDefault();
                    e.stopPropagation();
                    items[active].click();
                    return;
                }
                if (isSelect(target) && e.key.length === 1) {
                    var now = Date.now();
                    typed = now - typedAt > 700 ? e.key : typed + e.key;
                    typedAt = now;
                    var found = items.findIndex(function (item) {
                        return item.textContent.trim().toLocaleLowerCase("pt-BR").indexOf(typed.toLocaleLowerCase("pt-BR")) === 0;
                    });
                    if (found >= 0) { setActive(found); }
                    e.preventDefault();
                    return;
                }
            }
            if (isDateField(target) && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); close(); return; }
            return;
        }
        if (!field) { return; }
        var opens = e.key === "Enter" || e.key === " " || e.key === "F4" || (isSelect(field) && (e.key === "ArrowDown" || e.key === "ArrowUp")) || (e.altKey && e.key === "ArrowDown");
        if (opens && !(e.key === "Enter" && isDateField(field))) {
            e.preventDefault();
            e.stopPropagation();
            openField(field);
        } else if (isSelect(field) && e.key.length === 1 && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            openField(field);
        }
    }, true);

    document.addEventListener("focusin", function (e) {
        if (e.target instanceof HTMLInputElement && e.target.getAttribute("list")) { adoptDatalist(e.target); }
        if (isDateField(e.target)) { dateCommitted.set(e.target, e.target.value); }
    }, true);

    // Data digitada: enquanto os segmentos estão pela metade o campo vale vazio, e o @bind trocaria esse vazio por 01/01/0001.
    // O que vem do teclado fica retido e só chega ao Blazor ao sair do campo ou no Enter. A escolha pelo calendário passa direto.
    var dateCommitted = new WeakMap();

    function commitDate(el) {
        var last = dateCommitted.has(el) ? dateCommitted.get(el) : el.value;
        if (el.validity.badInput || (el.value === "" && el.required)) {
            // Num formulário o campo fica como está e a validação avisa ao salvar. Fora dele, nada avisaria: volta ao último valor.
            if (!el.form) { el.value = last; }
            return;
        }
        if (el.value !== last) {
            el.dispatchEvent(new Event("input", { bubbles: true }));
            el.dispatchEvent(new Event("change", { bubbles: true }));
        }
    }

    function holdTypedDate(e) {
        if (!isDateField(e.target)) { return; }
        if (e.isTrusted) {
            hideTip();
            // Quem digita não usa o calendário, que ficaria mostrando a data antiga sobre os campos.
            if (anchor === e.target) { close(); }
            e.stopPropagation();
        } else {
            dateCommitted.set(e.target, e.target.value);
        }
    }

    // No window: o Blazor escuta change na captura do document, e parar ali não impediria o handler dele.
    window.addEventListener("input", holdTypedDate, true);
    window.addEventListener("change", holdTypedDate, true);
    document.addEventListener("focusout", function (e) {
        if (isDateField(e.target)) { commitDate(e.target); }
    }, true);
    document.addEventListener("keydown", function (e) {
        if (e.key === "Enter" && isDateField(e.target)) { commitDate(e.target); }
    }, true);

    document.addEventListener("input", function (e) {
        hideTip();
        if (e.target instanceof HTMLInputElement) {
            if (e.target.getAttribute("list")) { adoptDatalist(e.target); }
            if (e.target.dataset.suggestions) { drawSuggestions(e.target); }
        }
    }, true);

    document.addEventListener("focusout", function (e) {
        if (popup && e.target === anchor && !(e.relatedTarget && popup.contains(e.relatedTarget))) { close(); }
    }, true);

    window.addEventListener("resize", close);
    document.addEventListener("scroll", function (e) {
        if (popup && !popup.contains(e.target)) { close(); }
        hideTip();
    }, true);

    // Validação: a mensagem aparece no padrão da aplicação, não no balão do navegador.
    var lastInvalid = 0;
    document.addEventListener("invalid", function (e) {
        e.preventDefault();
        var now = Date.now();
        if (now - lastInvalid < 200) { return; }
        lastInvalid = now;
        var field = e.target;
        field.focus();
        showTip(field, field.validity && field.validity.valueMissing ? "Preencha este campo." : "Confira este campo: " + field.validationMessage, 3500);
    }, true);

    // Dica de title: o texto passa para um atributo próprio e aparece com os tokens.
    document.addEventListener("pointerover", function (e) {
        if (!(e.target instanceof Element)) { return; }
        var el = e.target.closest("[title]");
        if (el) {
            var text = el.getAttribute("title");
            el.removeAttribute("title");
            if (text) {
                el.setAttribute("data-tip", text);
                if (!el.hasAttribute("aria-label") && !el.textContent.trim()) { el.setAttribute("aria-label", text); }
            }
        }
        var owner = e.target.closest("[data-tip]");
        if (owner && e.pointerType === "mouse") { showTip(owner, owner.getAttribute("data-tip")); }
    }, true);

    document.addEventListener("pointerout", function (e) {
        if (e.target instanceof Element && e.target.closest("[data-tip]")) { hideTip(); }
    }, true);
})();
