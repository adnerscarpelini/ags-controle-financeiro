# Money e AccountingMonth

Implementa a issue [#3](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/3). A decisão de manter estes tipos no `SharedKernel` está em [shared-kernel.md](../architecture/shared-kernel.md).

## Money

Valor monetário em BRL, imutável (`readonly record struct`).

- **Arredondamento:** todo valor é arredondado para 2 casas, com `MidpointRounding.AwayFromZero`, ao ser criado e ao resultar de qualquer operação. `1,235` vira `1,24`; `-1,235` vira `-1,24`.
- **Operações:** soma, subtração, negação e multiplicação por um fator `decimal` (`+`, `-`, `-x`, `*`, e os equivalentes `Add`, `Subtract`, `Negate`, `Multiply`). O resultado da multiplicação é arredondado.
- **Comparação:** `<`, `<=`, `>`, `>=`, `CompareTo` e igualdade por valor. `IsZero` e `IsNegative` indicam o sinal.
- **Centavos:** `Money.FromCents(long)` e `ToCents()` convertem de e para centavos inteiros, que é como o valor é gravado no banco.
- **Formatação:** `ToString()` devolve o padrão pt-BR (`R$ 1.234,56`; negativos `-R$ 1.234,56`), sem depender da cultura da máquina.
- **Divisão em partes:** `Split(n)` devolve `n` partes cuja soma é exatamente o total. A parte regular é `total / n` arredondada; a primeira parte recebe o que sobra, ou seja, `total - parte regular × (n - 1)`.

| Total | Partes | Resultado |
| --- | --- | --- |
| R$ 3.779,91 | 10 | R$ 378,00 e nove de R$ 377,99 |
| R$ 100,00 | 3 | R$ 33,34, R$ 33,33 e R$ 33,33 |
| R$ 1,09 | 10 | R$ 0,10 e nove de R$ 0,11 |

`Split` com `n <= 0` lança `ArgumentOutOfRangeException`.

## AccountingMonth

Ano e mês em que um valor pesa no orçamento (`readonly record struct`).

- **Criação:** `new AccountingMonth(year, month)` rejeita mês fora de 1 a 12 e ano fora de 1 a 9999, com `ArgumentOutOfRangeException`. `AccountingMonth.FromDate(DateOnly)` usa o ano e o mês da data.
- **Navegação:** `AddMonths(n)` aceita valores negativos e atravessa a virada de ano (12/2026 + 1 = 01/2027; 01/2026 − 1 = 12/2025). Um resultado fora do intervalo suportado lança `ArgumentOutOfRangeException`.
- **Limites do mês:** `FirstDay` e `LastDay` (respeitam anos bissextos).
- **Comparação:** por ano e depois por mês (`<`, `<=`, `>`, `>=`, `CompareTo`).
- **Formatação:** `ToString()` devolve `MM/yyyy`.

O cálculo do mês contábil de uma compra no cartão **não** está aqui: pertence ao módulo `Accounts`.

## Limitação conhecida

Como são `struct`, `default(AccountingMonth)` existe e tem ano e mês zerados, o que não é um mês válido. Código de aplicação não deve usá-lo; `default(Money)` é válido e igual a `Money.Zero`.
