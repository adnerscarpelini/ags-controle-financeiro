# SharedKernel

## Contexto

A regra do projeto evita código compartilhado entre módulos até que haja duplicação real. Mas dois conceitos aparecem em quase todos os módulos desde o primeiro dia: **dinheiro** e **mês de referência**. Duplicá-los criaria versões divergentes da mesma regra.

## Decisão

`Ags.Finance.SharedKernel` contém **apenas**:

- `Money`: valor em `decimal` na moeda BRL, com arredondamento explícito em 2 casas e operações aritméticas.
- `ReferenceMonth`: ano e mês, com comparação, avanço de meses e formatação.

Ambos são objetos de valor imutáveis, sem dependências além da BCL. Qualquer tipo novo no `SharedKernel` exige uma nova nota nesta pasta.

## Consequências

- Todos os módulos referenciam o `SharedKernel`; ele não referencia nenhum módulo.
- A regra de cálculo do mês de referência de uma compra no cartão **não** fica aqui: ela pertence ao módulo `Accounts`, que conhece o calendário de cada cartão.

## Renomeação (#90)

A [#90](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/90) troca o
nome do mês por ReferenceMonth sem alterar o calendário ou o inteiro `yyyyMM`.
O deslocamento da recorrência é renomeado com `RenameColumn`, preservando valores.
As demais colunas já se chamam `Month`/`FirstMonth`; conversores mudam de nome, mas
seus dados e tipos não mudam e não exigem migrations vazias. Migrations históricas
ficam intactas. O CSV passa à versão 3, com leitura estrita da nova coluna; versões
anteriores são rejeitadas conforme a política do layout.
