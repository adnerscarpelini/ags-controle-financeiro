# Design da interface

## Decisão

Visual moderno e limpo, com **tema escuro como padrão**, fundo neutro e um **verde "dinheiro"** reservado à ação principal, em Blazor com [MudBlazor](ui-library.md). Todas as cores vêm de tokens declarados como variáveis CSS num único arquivo (`--color-accent`, `--color-background`…). A interface é responsiva e instalável como PWA (#38). O tema pode ser escuro, claro ou automático ([preferências](../preferences/settings.md)).

**Estado:** layout, tokens e navegação implementados (#5), com a nova interface do lote #95 a #103 e a linguagem visual da #112; ver [layout-and-navigation.md](../web/layout-and-navigation.md).

## Princípios de todas as telas

Aprovados na [#95](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/95):

- **Muito fácil de usar:** cada tela tem um objetivo claro e uma única ação principal em destaque.
- **Clean, moderna e minimalista, mas poderosa:** o essencial primeiro, com filtros, opções e detalhes a um clique, sem remover funcionalidades.
- **Textos explícitos:** títulos, menus e botões dizem o que fazem, como "Nova despesa", e evitam termos técnicos.
- **Ícones com significado:** sempre com rótulo visível nos menus. Botão só com ícone exige tooltip e nome acessível.
- **Tooltip em tudo que possa gerar dúvida:** campos, situações, totais e ações com efeito financeiro, com mouse, teclado e toque (`HelpTip`).
- **Tabelas como padrão de consulta:** ordenação, paginação e ações por linha. Formulários abrem somente quando pedidos.
- **Estados claros:** tela vazia com orientação e ação (`EmptyState`), carregamento, sucesso e erro em linguagem simples.

## Linguagem visual

Aprovada na [#112](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/112), com referência em Todoist, Notion e aplicativos da Apple.

- **Separação suave:** fundo neutro, com seções, totais e entidades em cards suaves, sem borda e sem sombra. Nunca card dentro de card; dentro de um card, a separação é por espaço e linhas finas. Métricas sem ícone decorativo.
- **Verde só onde conta:** ação principal, foco, item selecionado e receitas. Abas e filtros usam controle segmentado neutro.
- **Valores:** em toda a aplicação, despesa em vermelho, receita em verde, saldo pelo sinal e zero em cor neutra (`ValueColor`). Saldo restante de fatura fica vermelho e crédito verde; pagamento de fatura e transferência são neutros. Metas de planejamento são orçamento e ficam neutras.
- **Tipografia:** Inter local, 14 px no corpo e 22 px nos títulos, sem caixa alta, com hierarquia por tamanho e peso.
- **Controles:** 32 px no computador e 44 px no celular, campos preenchidos sem borda e botões sem sombra.
- **Tabelas:** cabeçalho discreto, linhas finas e ações reveladas ao passar o mouse ou receber foco. Em toque, as ações ficam em menu.
- **Ajuda:** todas as explicações continuam em `HelpTip`, com o ícone discreto.

## Dashboard

Acima de "Despesas por categoria" ficam dois cards lado a lado (#112): as faturas de cada cartão no mês, com vencimento, valor a pagar, total gasto, abatimentos (pagamentos, antecipações, cashback e descontos), limite usado e total a pagar, e o saldo atual e previsto de cada conta corrente. Poupança e investimentos não aparecem nem entram no total. O detalhe por coluna das faturas continua em "Faturas por cartão". Os cards seguem as preferências de seção Faturas e Resumo do mês.

## Controles sem aparência do sistema

Nenhum controle aberto pode ter a aparência do Windows ou do navegador. `wwwroot/js/pickers.js` mantém os elementos HTML e o `@bind` do Blazor, mas substitui a parte aberta: lista do select, calendário, seletor de mês, sugestões de `datalist`, mensagens de validação e dicas de `title`. Ao escolher, grava o valor no campo e dispara `input` e `change`.

A exceção é a janela de escolha de arquivos, que pertence ao sistema operacional. O campo em si segue os tokens.

## Paleta

| Token | Claro | Escuro | Uso |
| --- | --- | --- | --- |
| `Background` | `#FFFFFF` | `#141415` | fundo da janela |
| `Sidebar` | `#F7F7F6` | `#1A1A1C` | menu lateral |
| `Card` | `#F7F7F6` | `#1B1B1D` | cards suaves de seções, totais e entidades |
| `Surface` | `#FEFEFE` | `#222224` | menus e janelas |
| `SurfaceRaised` | `#F2F2F1` | `#2C2C2F` | superfície neutra sólida |
| `Border` | `#E8E8E7` | `#2C2C2E` | linhas finas e divisórias |
| `BorderStrong` | `#D1D1CF` | `#48484A` | bordas tracejadas e controles |
| `TextPrimary` | `#1D1D1F` | `#F5F5F7` | texto principal e despesas |
| `TextSecondary` | `#6A6A6F` | `#A1A1A6` | rótulos e texto secundário |
| `Accent` | `#0B7A55` | `#30C48D` | ação principal, foco e item selecionado |
| `AccentHover` | `#09684A` | `#4AD6A1` | hover do accent |
| `Income` | `#0A7450` | `#4AD6A2` | receitas |
| `Expense` | `#C4372E` | `#FF8177` | atraso, excesso e erro |
| `Warning` | `#946200` | `#E5B13A` | pendências e alertas |

Cores de banco, só no texto: Itaú `#FB923C`, Nubank `#A78BFA`, Bradesco `#F87171` (no tema claro: `#9A3412`, `#6D28D9` e `#B91C1C`).

Tokens translúcidos (`--color-fill`, `--color-fill-strong` e os `-soft`) cuidam de campos, hover e estados neutros. Os valores completos estão em `tokens.css`.

## Regras

- Fonte Inter servida pela própria aplicação; nada é carregado de CDN.
- Abaixo de 768 px de largura, o menu lateral vira uma gaveta com a mesma árvore e o conteúdo ocupa a tela inteira, sem rolagem horizontal da página. O **+ Novo** fica sempre acessível, em botão flutuante. Alvos de toque com pelo menos 44 px.
- No celular, janelas de formulário ocupam a tela inteira, com as ações fixas no rodapé (`AppDialogs`). Tabelas viram linhas com rótulos (`AppTables`); em Lançamentos, cada linha mostra descrição, data, situação e valor, com as ações em menu e a ordenação em um seletor. Planejamento e a visão de 19 meses mantêm a primeira coluna fixa, com os meses rolando na horizontal.
- Gráficos com uma biblioteca web escolhida na issue que os introduz, usando as mesmas cores. Valores com dígitos tabulares e alinhados à direita. O mapa de calor mostra a unidade uma vez e `-` em mês sem valor.
- Cultura `pt-BR`: `R$ 1.234,56` e `dd/MM/yyyy`.
- Cantos de 8 em controles, 12 em cards e 14 em janelas, espaçamento em múltiplos de 4, sombras leves só em menus e janelas.
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
