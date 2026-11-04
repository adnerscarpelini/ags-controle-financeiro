// Tema (#69, #71). O servidor já entrega o tema salvo no HTML inicial; aqui ficam a troca sem recarregar a página
// (configurações) e a cor da barra do navegador no celular, que acompanha o tema usando a cor de fundo calculada
// (sem valores literais de cor fora de tokens.css).
(function () {
    'use strict';

    var systemDark = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
    var systemListeners = new Map();

    function syncThemeColor() {
        if (!document.body) {
            return;
        }

        var meta = document.querySelector('meta[name="theme-color"]');
        if (!meta) {
            meta = document.createElement('meta');
            meta.setAttribute('name', 'theme-color');
            document.head.appendChild(meta);
        }

        meta.setAttribute('content', window.getComputedStyle(document.body).backgroundColor);
    }

    window.agsTheme = {
        apply: function (theme) {
            if (theme === 'light' || theme === 'dark' || theme === 'auto') {
                document.documentElement.dataset.theme = theme;
                syncThemeColor();
            }
        },
        // Avisa o layout quando o sistema muda, para o modo automático trocar a paleta dos componentes.
        watchSystem: function (reference) {
            if (!systemDark) {
                return true;
            }

            var listener = function (event) {
                reference.invokeMethodAsync('SystemThemeChanged', event.matches);
            };
            systemListeners.set(reference, listener);
            systemDark.addEventListener('change', listener);
            return systemDark.matches;
        },
        unwatchSystem: function (reference) {
            var listener = systemListeners.get(reference);
            if (listener) {
                systemDark.removeEventListener('change', listener);
                systemListeners.delete(reference);
            }
        },
    };

    syncThemeColor();
    document.addEventListener('DOMContentLoaded', syncThemeColor);
    if (systemDark) {
        systemDark.addEventListener('change', syncThemeColor);
    }

    // A navegação melhorada do Blazor refaz o <head>: a cor é calculada de novo a cada página.
    if (window.Blazor && window.Blazor.addEventListener) {
        window.Blazor.addEventListener('enhancedload', syncThemeColor);
    }
}());
