# SharedKernel

## Contexto

A regra do projeto evita código compartilhado entre módulos até que haja duplicação real. Mas dois conceitos aparecem em quase todos os módulos desde o primeiro dia: **dinheiro** e **mês contábil**. Duplicá-los criaria versões divergentes da mesma regra.

## Decisão

`Ags.Finance.SharedKernel` contém **apenas**:

- `Money`: valor em `decimal` na moeda BRL, com arredondamento explícito em 2 casas e operações aritméticas.
- `AccountingMonth`: ano e mês, com comparação, avanço de meses e formatação.

Ambos são objetos de valor imutáveis, sem dependências além da BCL. Qualquer tipo novo no `SharedKernel` exige uma nova nota nesta pasta.

## Consequências

- Todos os módulos referenciam o `SharedKernel`; ele não referencia nenhum módulo.
- A regra de cálculo do mês contábil de uma compra no cartão **não** fica aqui: ela pertence ao módulo `Accounts`, que conhece o calendário de cada cartão.
