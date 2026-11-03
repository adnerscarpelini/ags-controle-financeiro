// Informa ao servidor se a tela está na largura do celular (abaixo de 768 px), o mesmo limite do CSS.
(function () {
    'use strict';

    var query = window.matchMedia('(max-width: 767.98px)');
    var listeners = new Map();

    // No iPhone, tocar fora de um botão não tira o foco dele: sem isto, a ajuda aberta por toque não fecharia.
    document.addEventListener('pointerdown', function (event) {
        var active = document.activeElement;
        if (active && active.classList.contains('help-tip-button') && !active.contains(event.target)) {
            active.blur();
        }
    });

    // Menu recolhido: a escolha é por navegador. Sem armazenamento (janela privada, dados bloqueados), vale até recarregar.
    window.agsMenu = {
        isCollapsed: function () {
            try {
                return window.localStorage.getItem('ags.menu-collapsed') === '1';
            } catch (error) {
                return false;
            }
        },
        setCollapsed: function (collapsed) {
            try {
                if (collapsed) {
                    window.localStorage.setItem('ags.menu-collapsed', '1');
                } else {
                    window.localStorage.removeItem('ags.menu-collapsed');
                }
            } catch (error) {
                // a escolha vale só até recarregar
            }
        },
    };

    window.agsViewport = {
        watch: function (reference) {
            var listener = function (event) {
                reference.invokeMethodAsync('ViewportChanged', event.matches);
            };
            listeners.set(reference, listener);
            query.addEventListener('change', listener);
            return query.matches;
        },
        unwatch: function (reference) {
            var listener = listeners.get(reference);
            if (listener) {
                query.removeEventListener('change', listener);
                listeners.delete(reference);
            }
        },
    };
}());
