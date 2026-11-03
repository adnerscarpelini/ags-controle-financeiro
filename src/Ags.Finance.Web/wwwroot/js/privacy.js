// Ocultar valores (#71): troca todo valor monetário da tela por "R$ ••••", em qualquer página, sem alterar tela por tela.
// A escolha é por dispositivo (localStorage). O script roda no <head> para o observador pegar os nós já durante a leitura
// da página e os valores não aparecerem antes de serem ocultados. É só visual: não protege contra quem tem acesso ao navegador.
(function () {
    'use strict';

    var STORAGE_KEY = 'ags.hide-values';
    var MONEY = /-?R\$[\s ]?\d{1,3}(?:\.\d{3})*,\d{2}/g; // R$ 1.234,56 e -R$ 50,00 (Money.ToString)
    var MASK = 'R$ ••••';
    var ATTRIBUTES = ['title', 'aria-label'];
    var SKIPPED = { SCRIPT: 1, STYLE: 1, TEXTAREA: 1, NOSCRIPT: 1 };

    var textOriginals = new WeakMap();
    var attributeOriginals = new WeakMap(); // elemento -> { atributo: valor original }
    var hidden = readStored();
    var observer;

    function readStored() {
        try {
            return window.localStorage.getItem(STORAGE_KEY) === '1';
        } catch (error) {
            return false; // sem armazenamento (janela privada, dados bloqueados): valores visíveis
        }
    }

    function store(value) {
        try {
            if (value) {
                window.localStorage.setItem(STORAGE_KEY, '1');
            } else {
                window.localStorage.removeItem(STORAGE_KEY);
            }
        } catch (error) {
            // a escolha vale só até recarregar
        }
    }

    function maskText(node) {
        var parent = node.parentNode;
        if (!parent || SKIPPED[parent.nodeName]) {
            return;
        }

        var value = node.data;
        if (value.search(MONEY) < 0) {
            return;
        }

        textOriginals.set(node, value);
        node.data = value.replace(MONEY, MASK);
    }

    function maskAttributes(element) {
        ATTRIBUTES.forEach(function (name) {
            var value = element.getAttribute(name);
            if (value === null || value.search(MONEY) < 0) {
                return;
            }

            var saved = attributeOriginals.get(element) || {};
            saved[name] = value;
            attributeOriginals.set(element, saved);
            element.setAttribute(name, value.replace(MONEY, MASK));
        });
    }

    function maskTree(root) {
        if (root.nodeType === 3) {
            maskText(root);
            return;
        }

        if (root.nodeType !== 1) {
            return;
        }

        maskAttributes(root);
        root.querySelectorAll('[title],[aria-label]').forEach(maskAttributes);
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        var node = walker.nextNode();
        while (node) {
            maskText(node);
            node = walker.nextNode();
        }
    }

    function unmaskAll() {
        var walker = document.createTreeWalker(document.documentElement, NodeFilter.SHOW_TEXT);
        var node = walker.nextNode();
        while (node) {
            if (textOriginals.has(node)) {
                node.data = textOriginals.get(node);
                textOriginals.delete(node);
            }

            node = walker.nextNode();
        }

        document.querySelectorAll('[title],[aria-label]').forEach(function (element) {
            var saved = attributeOriginals.get(element);
            if (!saved) {
                return;
            }

            Object.keys(saved).forEach(function (name) {
                element.setAttribute(name, saved[name]);
            });
            attributeOriginals.delete(element);
        });
    }

    function syncButtons() {
        var label = hidden ? 'Mostrar valores' : 'Ocultar valores';
        document.querySelectorAll('[data-privacy-toggle]').forEach(function (button) {
            button.setAttribute('aria-pressed', hidden ? 'true' : 'false');
            button.setAttribute('aria-label', label);
            var text = button.querySelector('[data-privacy-label]');
            if (text && text.textContent !== label) {
                text.textContent = label;
            }
        });
    }

    function onMutations(records) {
        if (hidden) {
            records.forEach(function (record) {
                if (record.type === 'childList') {
                    record.addedNodes.forEach(maskTree);
                } else if (record.type === 'characterData') {
                    // O Blazor trocou o texto: o original guardado deixou de valer.
                    textOriginals.delete(record.target);
                    maskText(record.target);
                } else if (record.type === 'attributes') {
                    var saved = attributeOriginals.get(record.target);
                    if (saved) {
                        delete saved[record.attributeName];
                    }

                    maskAttributes(record.target);
                }
            });
        }

        syncButtons();
        observer.takeRecords(); // descarta as mutações provocadas por este próprio script
    }

    function applyState() {
        if (hidden) {
            maskTree(document.documentElement);
        } else {
            unmaskAll();
        }

        syncButtons();
        observer.takeRecords();
    }

    function setHidden(value) {
        hidden = !!value;
        store(hidden);
        applyState();
    }

    observer = new MutationObserver(onMutations);
    observer.observe(document, {
        childList: true,
        subtree: true,
        characterData: true,
        attributes: true,
        attributeFilter: ATTRIBUTES,
    });

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (target && target.closest && target.closest('[data-privacy-toggle]')) {
            setHidden(!hidden);
        }
    });

    // Outra aba do mesmo navegador mudou a escolha.
    window.addEventListener('storage', function (event) {
        if (event.key === STORAGE_KEY) {
            hidden = readStored();
            applyState();
        }
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyState);
    } else {
        applyState();
    }

    window.agsPrivacy = {
        isHidden: function () { return hidden; },
        set: setHidden,
        toggle: function () { setHidden(!hidden); },
    };
}());
