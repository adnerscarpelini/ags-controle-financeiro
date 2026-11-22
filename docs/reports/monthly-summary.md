# Orçamento do mês

A [#24](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/24) abre o
dashboard com o resumo de um mês de referência (`IMonthlySummaries`, em
`Reports.Contracts`; totais da faixa Mês em `/dashboard`).

**Base de apuração.** Reports lê os totais pelo contrato `IEntryTotals` de
Transactions: soma por mês de referência, tipo e subcategoria, em uma consulta, sem acessar
tabelas de outro módulo. É a mesma base usada pelos demais relatórios do dashboard
(`Ledger`), menos as despesas por categoria, que seguem o
[comportamento do mês](monthly-behavior.md). Antes de ler, as ocorrências recorrentes dos meses de janeiro até o mês
selecionado são garantidas, como a tela de Lançamentos faz ao consultar um mês;
meses futuros não são gerados por causa do relatório.

**Cards.** Receitas e **A pagar no mês** (as despesas do mês de referência), nos totais da
faixa Mês ([#143](dashboard.md)), ao lado do Gasto no mês do [comportamento](monthly-behavior.md).
O contrato `MonthlySummary` tem `Month`, `Income` e `Expenses`. Saldo do orçamento, % guardado,
despesas do mês anterior e média no ano saíram do Dashboard e do contrato na #143; o acompanhamento
do orçamento fica no [planejamento mensal](monthly-planning.md).

**O que entra.** Receitas e despesas lançadas no mês de referência, **efetivadas e
pendentes, cada uma uma vez**. Transferências ficam de fora (não são lançamentos de
receita/despesa). Ajustes de fatura ([#52](../invoices/total-adjustments.md)) entram
como despesa uma única vez: acréscimo aumenta as despesas, redução as diminui, sem
virar receita.

**Rótulos.** Esses totais são do **orçamento mensal**: tudo o que foi lançado no mês de
referência. Não são saldo efetivo de conta (que só considera efetivados) nem previsão de caixa.

**Decisão em aberto.** A #24 pede fechar como pendentes aparecem nos indicadores
antes de combinar "realizado" e "previsto". Esta entrega não cria essa combinação:
usa a mesma regra dos totais da tela de Lançamentos (todo lançamento do mês, uma
vez). Quando a regra for decidida, ela muda só o `Ledger`.

A janela de próximos dias e atrasados é da #58, fica na faixa Hoje e não depende deste resumo.
