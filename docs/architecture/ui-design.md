# Design da interface

## Decisão

Visual moderno e limpo, com **tema escuro como padrão** e um **verde "dinheiro"** como cor principal, em Blazor. Todas as cores vêm de tokens declarados como variáveis CSS num único arquivo (`--color-accent`, `--color-background`…). A interface é responsiva (computador e celular) e instalável como PWA.

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
