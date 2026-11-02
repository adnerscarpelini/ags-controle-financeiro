# Design da interface

## Decisão

Visual moderno e limpo, com **tema escuro como padrão** e um **verde "dinheiro"** como cor principal. A base é o `FluentTheme` do Avalonia com `RequestedThemeVariant="Dark"`. Todas as cores vêm de tokens num `ResourceDictionary` central.

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

- Fonte Inter, embarcada no app. Valores com dígitos tabulares e alinhados à direita.
- Cultura `pt-BR`: `R$ 1.234,56` e `dd/MM/yyyy`.
- Cantos arredondados de 8, espaçamento em múltiplos de 4, sem sombras pesadas nem gradientes.
- Cor nunca é o único sinal: valores negativos também levam o sinal de menos.
- O tema claro pode ser adicionado depois, reaproveitando os mesmos tokens.
