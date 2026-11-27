# Design da interface

## Decisão

Visual moderno e marcante, com **tema escuro como padrão**, fundo grafite com leve tom violeta e um **roxo** como cor de marca, reservado à ação principal, ao foco e à seleção ([#154](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/154)), em Blazor com [MudBlazor](ui-library.md). Todas as cores vêm de tokens declarados como variáveis CSS num único arquivo (`--color-accent`, `--color-background`…). A interface é responsiva e instalável como PWA (#38). O tema pode ser escuro, claro ou automático ([preferências](../preferences/settings.md)).

**Estado:** layout, tokens e navegação implementados (#5), com a nova interface do lote #95 a #103, a linguagem visual da #112 e a identidade roxa da [#154](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/154); ver [layout-and-navigation.md](../web/layout-and-navigation.md).

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

Aprovada na [#112](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/112) e renovada na [#154](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/154), que trocou só a aparência: as mesmas telas, cards, tabelas e ações.

- **Cards com profundidade:** seções, totais e entidades em cards com borda fina (`Border`) e brilho interno sutil no escuro (`--shadow-card`). Nunca card dentro de card: dentro de um card, os totais viram caixas internas em `Fill`, e a separação é por espaço e linhas finas.
- **Um só destaque:** o card de saldo das contas correntes do Dashboard (`panel-highlight`) tem um brilho roxo radial no canto. Nenhum outro elemento usa degradê.
- **Roxo só onde conta:** ação principal (fundo `AccentSolid`, texto branco), foco, item selecionado, chip ativo, link e ícone do item ativo do menu. Verde fica para as receitas e vermelho para as despesas.
- **Chips e etiquetas:** abas e filtros são pílulas com borda fina; a ativa ganha `AccentSoft`. Situações (Aberta, Fechada, Paga, Pendente, Atrasado) são etiquetas com bolinha e fundo suave da cor da situação (`tag-open`, `tag-closed`, `tag-paid`, `tag-pending`, `tag-late`).
- **Ícones de assunto:** no Dashboard, o título de cada card tem um ícone num quadrado suave (`heading-icon`); as pendências mostram a data num bloco com dia e mês.
- **Valores:** em toda a aplicação, despesa em vermelho, receita em verde, saldo pelo sinal e zero em cor neutra (`ValueColor`). Saldo restante de fatura fica vermelho e crédito verde; pagamento de fatura e transferência são neutros. Metas de planejamento são orçamento e ficam neutras.
- **Tipografia:** Geist local (licença OFL), dígitos tabulares, 14 px no corpo, 30 px no título da página, 20 px nas faixas e 34–36 px nos valores de destaque, com espaçamento negativo nos títulos.
- **Controles:** 36 px no computador e 44 px no celular; campos de 40 px preenchidos sem borda, com anel roxo no foco e vermelho no erro; botão principal com brilho interno e sombra difusa roxa.
- **Tabelas:** cabeçalho em caixa alta pequena, linhas finas, valores em negrito e ações reveladas ao passar o mouse ou receber foco. Em toque, as ações ficam em menu. A situação aparece num círculo suave da cor dela.
- **Botões:** sempre à direita, no topo ou no rodapé de telas, janelas e formulários (`page-head-actions`, `form-actions`, `mud-dialog-actions`). Exceções: login, estado vazio centralizado e ações de um card ou linha.
- **Ajuda:** todas as explicações continuam em `HelpTip`, com o ícone discreto.

## Dashboard

O Dashboard tem duas faixas (#143). **Hoje**, sem mês, abre com dois cards lado a lado: o saldo atual das contas correntes, com o total em destaque (poupança e investimentos não aparecem nem entram no total), e as faturas a pagar (#144), com o restante de cada fatura em destaque e "Total · abatido" abaixo; seguem os próximos dias e os atrasados. **Mês** tem o seletor de mês à direita do título da faixa, os totais do mês (Receitas, Gasto no mês e A pagar no mês) e as tabelas de categorias. Detalhes em [Dashboard: faixas Hoje e Mês](../reports/dashboard.md).

## Recorte do logo

A imagem aparece numa área quadrada com o círculo final marcado: arrastar posiciona, a roda do mouse e o controle deslizante mudam o zoom, e "Preencher" ou "Ajustar" definem o enquadramento. O arquivo é lido direto no navegador só para exibir (`js/logo-crop.js`); o recorte salvo continua no servidor, com os mesmos valores de zoom e posição. Pelo teclado, a posição segue disponível em controles de intervalo.

## Controles sem aparência do sistema

Nenhum controle aberto pode ter a aparência do Windows ou do navegador. `wwwroot/js/pickers.js` mantém os elementos HTML e o `@bind` do Blazor, mas substitui a parte aberta: lista do select, calendário, seletor de mês, sugestões de `datalist`, mensagens de validação e dicas de `title`. Ao escolher, grava o valor no campo e dispara `input` e `change`.

A data digitada não chega ao Blazor a cada tecla ([#127](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/127)). Enquanto os segmentos estão pela metade o campo nativo vale vazio, e o `@bind` de um `DateOnly` trocaria esse vazio por 01/01/0001. `pickers.js` retém os eventos do teclado e só entrega o valor ao sair do campo ou no Enter. Data incompleta ou obrigatória vazia não é entregue: num formulário a validação avisa ao salvar; fora dele o campo volta ao último valor. Campo ligado a `DateOnly` sem nulo precisa de `required`.

A exceção é a janela de escolha de arquivos, que pertence ao sistema operacional. O campo em si segue os tokens.

Os campos de valor formatam em reais enquanto se digita, com os dígitos entrando pela direita: 123456 vira `1.234,56` (`wwwroot/js/money-mask.js`, [#121](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/121)). Ao entrar no campo, o valor fica selecionado, e os dígitos entram sempre pelo fim, mesmo depois de clicar no meio ou usar as setas. Onde o campo aceita negativo, o sinal de menos inverte o sinal. O script também devolve o foco ao campo em AutoFocus, porque a janela da biblioteca e o menu que a abriu o tiram dele.

## Paleta

| Token | Claro | Escuro | Uso |
| --- | --- | --- | --- |
| `Background` | `#F7F7FA` | `#0B0B10` | fundo da janela |
| `Sidebar` | `#FFFFFF` | `#0F0F16` | menu lateral |
| `Card` | `#FFFFFF` | `#14141C` | cards de seções, totais e entidades |
| `Surface` | `#FFFFFF` | `#1B1B25` | menus, listas abertas e janelas |
| `SurfaceRaised` | `#F1F1F6` | `#22222E` | superfície neutra sólida |
| `Border` | `#ECECF2` | `#24242E` | borda fina de cards, linhas e divisórias |
| `BorderStrong` | `#D9D9E3` | `#3A3A48` | controles, chips, menus e bordas tracejadas |
| `TextPrimary` | `#16161E` | `#F3F3F8` | texto principal |
| `TextSecondary` | `#5D5D70` | `#9C9CB0` | rótulos e texto secundário |
| `Accent` | `#6544E0` | `#9A8AFF` | link, foco, item selecionado e chip ativo (texto e ícone) |
| `AccentHover` | `#5534D0` | `#B4A8FF` | hover do link |
| `AccentSolid` | `#6544E0` | `#7150F2` | fundo do botão principal, chave ligada e dia escolhido, com texto branco (`OnAccent`) |
| `AccentSolidHover` | `#5534D0` | `#6A47EA` | hover do botão principal |
| `Income` | `#0A7A4B` | `#3DDC97` | receitas |
| `Expense` | `#C82A43` | `#FF6B81` | despesas, atraso, excesso e erro |
| `Warning` | `#8F5E00` | `#F4B740` | pendências, fatura Fechada e alertas |

Cores de banco, só no texto: Itaú `#FB923C`, Nubank `#A78BFA`, Bradesco `#F87171` (no tema claro: `#9A3412`, `#6D28D9` e `#B91C1C`).

Tokens translúcidos (`--color-fill`, `--color-fill-strong`, `--color-accent-soft`, `--color-income-soft`, `--color-expense-soft`, `--color-warning-soft` e `--color-glow`) cuidam de campos, hover, etiquetas, ícones de situação e do brilho do card de destaque. As sombras ficam em `--shadow-card`, `--shadow-raised` e `--shadow-accent`. Os valores completos estão em `tokens.css`.

## Regras

- Fonte Geist servida pela própria aplicação (`wwwroot/fonts`, licença em `Geist-LICENSE.txt`); nada é carregado de CDN.
- Abaixo de 768 px de largura, o menu lateral vira uma gaveta com a mesma árvore e o conteúdo ocupa a tela inteira, sem rolagem horizontal da página. O **+ Novo** fica sempre acessível, em botão flutuante. Alvos de toque com pelo menos 44 px.
- No celular, janelas de formulário ocupam a tela inteira, com as ações fixas no rodapé (`AppDialogs`). Tabelas viram linhas com rótulos (`AppTables`): o rótulo fica inteiro à esquerda e, na descrição, título e observação ficam empilhados à direita ([#158](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/158)). O ícone de ajuda continua na linha do título da página; em Lançamentos, cada linha mostra descrição, data, situação e valor, com as ações em menu e a ordenação em um seletor. Planejamento e a visão de 19 meses mantêm a primeira coluna fixa, com os meses rolando na horizontal.
- Gráficos com uma biblioteca web escolhida na issue que os introduz, usando as mesmas cores. Valores com dígitos tabulares e alinhados à direita.
- Cultura `pt-BR`: `R$ 1.234,56` e `dd/MM/yyyy`.
- Cantos de 10 em controles, 16 em cards de total e entidades, 18 em cards de seção, 14 em menus abertos e 20 em janelas; espaçamento em múltiplos de 4; sombra forte só em menus e janelas.
- Cor nunca é o único sinal: valores negativos também levam o sinal de menos.
- O tema claro reaproveita os mesmos tokens: cada um é `light-dark(claro, escuro)` e `data-theme` (`dark`, `light` ou `auto`) no `<html>` escolhe o lado (#69). O escuro continua o padrão.
- Todo texto precisa de contraste mínimo 4,5:1 (WCAG AA) nos dois temas, inclusive o branco do botão principal sobre `AccentSolid`; `UiTokensTests` verifica.

## Logo

O arquivo `logo.png` na raiz do repositório (1254 × 1254 px, fundo transparente) é a imagem oficial do projeto, recolorida para o roxo na [#154](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/154): aparece no README e é a base do favicon e dos ícones do PWA, gerados a partir dele nos tamanhos necessários.

O favicon já está entregue; os ícones/manifesto instaláveis dependem da #38.

## Cadastros visuais implementados

- [Logo opcional de banco (#54)](../accounts/bank-logos.md): recorte circular com prévia, posição e zoom, PNG 256 × 256 com transparência externa e referência local autenticada. O contrato permite reutilização futura em relatórios.
- [Carteira visual (#55)](../accounts/card-wallet.md): componente HTML/CSS com dados atuais do cadastro, face de proporção aproximada de 1,6:1, detalhes e ações sempre visíveis. A grade se adapta ao celular, com foco e alvos de 44 pixels.
- [Arquivamento (#53)](../accounts/archiving.md): contas/cartões saem das listas padrão e novas seleções, com consulta e restauração explícitas. Histórico, saldos e faturas continuam acessíveis.

As imagens de logo enviadas pelo usuário são arquivos, não novos valores literais de cor
nos componentes. Cores institucionais em CSS continuam restritas ao texto conforme a regra acima.
