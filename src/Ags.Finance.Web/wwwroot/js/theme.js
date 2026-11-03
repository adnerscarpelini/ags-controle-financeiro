// Tema (#69, #71). O servidor já entrega o tema salvo no HTML inicial; aqui ficam a troca sem recarregar a página
// (configurações) e a cor da barra do navegador no celular, que acompanha o tema usando a cor de fundo calculada
// (sem valores literais de cor fora de tokens.css).
(function () {
    'use strict';

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
    };

    syncThemeColor();
    document.addEventListener('DOMContentLoaded', syncThemeColor);
    if (window.matchMedia) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', syncThemeColor);
    }

    // A navegação melhorada do Blazor refaz o <head>: a cor é calculada de novo a cada página.
    if (window.Blazor && window.Blazor.addEventListener) {
        window.Blazor.addEventListener('enhancedload', syncThemeColor);
    }
}());
