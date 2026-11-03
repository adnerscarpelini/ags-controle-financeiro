# Design da interface

## Decisão

Visual moderno e limpo, com **tema escuro como padrão**, fundo cinza neutro quase preto e um **verde "dinheiro"** como cor de destaque, em Blazor com [MudBlazor](ui-library.md). Todas as cores vêm de tokens declarados como variáveis CSS num único arquivo (`--color-accent`, `--color-background`…). A interface é responsiva e instalável como PWA (#38). O tema pode ser escuro, claro ou automático ([preferências](../preferences/settings.md)).

**Estado:** layout, tokens e navegação implementados (#5), com a nova interface do lote #95 a #103; ver [layout-and-navigation.md](../web/layout-and-navigation.md).

## Princípios de todas as telas

Aprovados na [#95](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/95):

- **Muito fácil de usar:** cada tela tem um objetivo claro e uma única ação principal em destaque.
- **Clean, moderna e minimalista, mas poderosa:** o essencial primeiro, com filtros, opções e detalhes a um clique, sem remover funcionalidades.
- **Textos explícitos:** títulos, menus e botões dizem o que fazem, como "Nova despesa", e evitam termos técnicos.
- **Ícones com significado:** sempre com rótulo visível nos menus. Botão só com ícone exige tooltip e nome acessível.
- **Tooltip em tudo que possa gerar dúvida:** campos, situações, totais e ações com efeito financeiro, com mouse, teclado e toque (`HelpTip`).
- **Tabelas como padrão de consulta:** ordenação, paginação e ações por linha. Formulários abrem somente quando pedidos.
- **Estados claros:** tela vazia com orientação e ação (`EmptyState`), carregamento, sucesso e erro em linguagem simples.

## Paleta

| Token | Cor | Uso |
| --- | --- | --- |
| `Background` | `#121212` | fundo da janela |
| `Surface` | `#1A1A1A` | cards, painéis, grade |
| `SurfaceRaised` | `#242424` | linha selecionada, menus, hover |
| `Border` | `#2E2E2E` | bordas e divisórias |
| `TextPrimary` | `#EDEDED` | texto principal |
| `TextSecondary` | `#A3A3A3` | rótulos e texto secundário |
| `Accent` | `#10B981` | cor principal: botões, item ativo, foco |
| `AccentHover` | `#34D399` | hover e estados ativos |
| `Income` | `#34D399` | receitas e valores positivos |
| `Expense` | `#F87171` | despesas e valores negativos |
| `Warning` | `#FBBF24` | alertas |

A tabela mostra o lado escuro. O lado claro de cada token está em `tokens.css`:
`#F4F7F5`, `#FFFFFF`, `#E8EFEB`, `#CBD7D0`, `#13201A`, `#4B5C54`, `#047857`, `#065F46`, `#047857`, `#B91C1C` e `#92400E`, na mesma ordem.

Cores de banco, só no texto: Itaú `#FB923C`, Nubank `#A78BFA`, Bradesco `#F87171` (no tema claro: `#9A3412`, `#6D28D9` e `#B91C1C`).

## Regras

- Fonte Inter servida pela própria aplicação; nada é carregado de CDN.
- Abaixo de 768 px de largura, o menu lateral vira uma gaveta com a mesma árvore e o conteúdo ocupa a tela inteira, sem rolagem horizontal da página. O **+ Novo** fica sempre acessível, em botão flutuante. Alvos de toque com pelo menos 44 px.
- No celular, janelas de formulário ocupam a tela inteira, com as ações fixas no rodapé (`AppDialogs`). Tabelas viram cards com rótulos (`AppTables`); em Lançamentos, cada card mostra descrição, data, situação e valor, com as ações em menu e a ordenação em um seletor. Planejamento e a visão de 19 meses mantêm a primeira coluna fixa, com os meses rolando na horizontal.
- Gráficos com uma biblioteca web escolhida na issue que os introduz, usando as mesmas cores. Valores com dígitos tabulares e alinhados à direita.
- Cultura `pt-BR`: `R$ 1.234,56` e `dd/MM/yyyy`.
- Cantos mais arredondados (12 em cards e painéis, 16 em janelas, 8 em itens menores), espaçamento em múltiplos de 4, sombras leves só em menus e janelas, sem gradientes.
- Cor nunca é o único sinal: valores negativos também levam o sinal de menos.
- O tema claro reaproveita os mesmos tokens: cada um é `light-dark(claro, escuro)` e `data-theme` (`dark`, `light` ou `auto`) no `<html>` escolhe o lado (#69). O escuro continua o padrão.
- Todo texto precisa de contraste mínimo 4,5:1 (WCAG AA) nos dois temas; `UiTokensTests` verifica.

## Logo

O arquivo `logo.png` na raiz do repositório (1254 × 1254 px, fundo transparente) é a imagem oficial do projeto: aparece no README e é a base do favicon e dos ícones do PWA, gerados a partir dele nos tamanhos necessários.

O favicon já está entregue; os ícones/manifesto instaláveis dependem da #38.

## Cadastros visuais implementados

- [Logo opcional de banco (#54)](../accounts/bank-logos.md): recorte circular com prévia, posição e zoom, PNG 256 × 256 com transparência externa e referência local autenticada. O contrato permite reutilização futura em relatórios.
- [Carteira visual (#55)](../accounts/card-wallet.md): componente HTML/CSS com dados atuais do cadastro, face de proporção aproximada de 1,6:1, detalhes e ações sempre visíveis. A grade se adapta ao celular, com foco e alvos de 44 pixels.
- [Arquivamento (#53)](../accounts/archiving.md): contas/cartões saem das listas padrão e novas seleções, com consulta e restauração explícitas. Histórico, saldos e faturas continuam acessíveis.

As imagens de logo enviadas pelo usuário são arquivos, não novos valores literais de cor
nos componentes. Cores institucionais em CSS continuam restritas ao texto conforme a regra acima.
