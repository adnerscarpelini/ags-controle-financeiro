# Biblioteca de componentes da interface

Origem: [#95](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/95).

## Contexto

A interface misturava cadastro e consulta, quase não tinha tabelas e obrigava a rolar a tela para encontrar os dados. O Blazor não era o limite: faltavam componentes consistentes de tabela, janela, menu e tooltip.

## Decisão

Adotar **MudBlazor 9.12.0** (licença MIT, com alvo `net10.0`) no Blazor Web App atual, em Interactive Server.

- `AddMudServices()` em `Program.cs`; provedores (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) em `MainLayout.razor`.
- CSS e JS vêm de `_content/MudBlazor/`, servidos pela própria aplicação. Nada de CDN nem de Google Fonts: a fonte continua a Inter local e os ícones são os SVG da biblioteca (`Icons.Material.Outlined.*`). `LayoutPagesTests` confere a ausência de referências externas.
- A página de login é HTML puro, sem circuito, e não carrega a biblioteca.
- `wwwroot/css/tokens.css` continua a fonte de verdade das cores. `Components/Layout/AppTheme.cs` repete os valores porque o `MudTheme` calcula variações e não aceita `var()`; `AppThemeTests` falha se os dois divergirem.
- O tema segue a preferência: `ThemeState` avisa o layout ao salvar em Configurações, e `auto` usa `MudThemeProvider.GetSystemDarkModeAsync`, acompanhando mudanças do sistema.
- Componentes comuns: `HelpTip` (ajuda com mouse, teclado e toque), `Common/EmptyState` e `Common/ConfirmDialog`.

## Alternativas

- **React:** exigiria criar uma API HTTP para tudo, refazer o login e os testes de tela e incluir Node no build, sem ganho direto para o usuário.
- **CSS próprio com componentes feitos à mão:** manteria a dependência zero, mas obrigaria a reescrever e manter tabela, janela, menu e tooltip acessíveis.

## Consequências

- Dependência nova de terceiros, com CSS de cerca de 600 KB e JS de 70 KB servidos localmente.
- As atualizações da biblioteca podem exigir ajustes, porque `app.css` refina classes `mud-*`.
- Testes de tela registram `AddMudServices()` e `JSInterop.Mode = JSRuntimeMode.Loose`.
