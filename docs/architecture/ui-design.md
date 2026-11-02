# Design da interface

## Decisão

Visual moderno e limpo, com **tema escuro como padrão** e um **verde "dinheiro"** como cor principal, em Blazor. Todas as cores vêm de tokens declarados como variáveis CSS num único arquivo (`--color-accent`, `--color-background`…). A interface é responsiva; instalação como PWA ainda depende da #38.

**Estado:** layout, tokens e navegação implementados (issue #5); ver [layout-and-navigation.md](../web/layout-and-navigation.md).

## Paleta

| Token | Cor | Uso |
| --- | --- | --- |
| `Background` | `#0E1311` | fundo da janela |
| `Surface` | `#151C19` | cards, painéis, grade |
| `SurfaceRaised` | `#1C2521` | linha selecionada, menus, hover |
| `Border` | `#27332E` | bordas e divisórias |
| `TextPrimary` | `#E6EDE9` | texto principal |
| `TextSecondary` | `#93A39B` | rótulos e texto secundário |
| `Accent` | `#10B981` | cor principal: botões, item ativo, foco |
| `AccentHover` | `#34D399` | hover e estados ativos |
| `Income` | `#34D399` | receitas e valores positivos |
| `Expense` | `#F87171` | despesas e valores negativos |
| `Warning` | `#FBBF24` | alertas |

Cores de banco, só no texto: Itaú `#FB923C`, Nubank `#A78BFA`, Bradesco `#F87171`.

## Regras

- Fonte Inter servida pela própria aplicação; nada é carregado de CDN.
- Abaixo de 768 px de largura, o menu lateral vira uma gaveta e o conteúdo ocupa a tela inteira. Alvos de toque com pelo menos 44 px.
- Gráficos com uma biblioteca web escolhida na issue que os introduz, usando as mesmas cores. Valores com dígitos tabulares e alinhados à direita.
- Cultura `pt-BR`: `R$ 1.234,56` e `dd/MM/yyyy`.
- Cantos arredondados de 8, espaçamento em múltiplos de 4, sem sombras pesadas nem gradientes.
- Cor nunca é o único sinal: valores negativos também levam o sinal de menos.
- O tema claro pode ser adicionado depois, reaproveitando os mesmos tokens.

## Logo

O arquivo `logo.png` na raiz do repositório (1254 × 1254 px, fundo transparente) é a imagem oficial do projeto: aparece no README e é a base do favicon e dos ícones do PWA, gerados a partir dele nos tamanhos necessários.

O favicon já está entregue; os ícones/manifesto instaláveis dependem da #38.

## Evoluções aprovadas, ainda não implementadas

- [Logo opcional de banco (#54)](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/54):
  recorte circular com prévia, posição e zoom, saída recomendada PNG 256 × 256 com
  transparência externa. Usar em relatórios e identificação do cartão, com alternativa textual.
- [Carteira visual (#55)](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/55):
  cartões desenhados em HTML/CSS com os dados do cadastro, responsivos, sem depender de hover
  ou imagem estática do cartão. Reutilizar tokens e não introduzir gradientes ou sombras pesadas.
- [Arquivamento (#53)](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/53):
  ocultar contas/cartões das listas padrão e novas seleções, oferecendo consulta e desarquivamento.
  Não ocultar os respectivos valores do histórico, relatórios e faturas pendentes.

As imagens de logo enviadas pelo usuário são arquivos, não novos valores literais de cor
nos componentes. Cores institucionais em CSS continuam restritas ao texto conforme a regra acima.
