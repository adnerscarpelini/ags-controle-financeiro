# Comportamento do mês

A [#117](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/117) separa duas
perguntas no dashboard. O [orçamento](monthly-summary.md) mostra o que cai no mês, incluindo
as faturas. O comportamento mostra quanto foi gasto no mês, para
controlar os gastos enquanto eles acontecem (`IMonthlyBehaviors`, em `Reports.Contracts`;
faixa Mês do dashboard).

**O que conta.** Toda despesa cuja data está no mês, em qualquer forma de pagamento, efetivada
ou pendente. O mês de referência não importa: uma compra no cartão feita em novembro com fatura
de dezembro conta em novembro. Nada gravado muda; é só outra forma de somar
(`IEntryTotals.GetSpendingByDateAsync`).

**Parcelas.** Todas as parcelas guardam a data da compra, então a compra parcelada conta inteira
no mês da compra e não reaparece nos meses seguintes.

**O que fica de fora.** Receitas, transferências e pagamentos de fatura. Ajustes de fatura
([#52](../invoices/total-adjustments.md)) não têm compra e contam uma vez, no mês de referência
deles.

**No Dashboard.** O valor aparece como **Gasto no mês** nos totais da faixa Mês
([#143](dashboard.md)). Os cards de mês anterior e média no ano saíram do Dashboard na #143, e o
contrato `MonthlyBehavior` ficou só com `Month` e `Spent`.

**Categorias.** A tabela e o gráfico de [despesas por categoria](category-report.md) usam esta
mesma base, então a soma das linhas é igual ao gasto do mês.

**Recorrências.** Antes de somar, as ocorrências com data no intervalo são garantidas, inclusive
as do cartão que caem na fatura do mês seguinte.

O card segue a preferência de seção "Contas e totais do mês".
