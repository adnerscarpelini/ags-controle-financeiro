# Resumo do mês

A [#24](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/24) abre o
dashboard com o resumo de um mês de referência (`IMonthlySummaries`, em
`Reports.Contracts`; cards em `/dashboard`).

**Base de apuração.** Reports lê os totais pelo contrato `IEntryTotals` de
Transactions: soma por mês de referência, tipo e subcategoria, em uma consulta, sem acessar
tabelas de outro módulo. É a mesma base usada pelos demais relatórios do dashboard
(`Ledger`). Antes de ler, as ocorrências recorrentes dos meses de janeiro até o mês
selecionado são garantidas, como a tela de Lançamentos faz ao consultar um mês;
meses futuros não são gerados por causa do relatório.

**Cards.** Receitas do mês; despesas do mês; saldo; % guardado (saldo ÷ receitas, `-`
quando não há receitas); despesas do mês anterior (dezembro do ano anterior para
janeiro) e média mensal de despesas no ano até o mês — soma das despesas dividida
pelo número de meses de janeiro até o mês selecionado que têm ao menos um lançamento
de despesa.

**O que entra.** Receitas e despesas lançadas no mês de referência, **efetivadas e
pendentes, cada uma uma vez**. Transferências ficam de fora (não são lançamentos de
receita/despesa). Ajustes de fatura ([#52](../invoices/total-adjustments.md)) entram
como despesa uma única vez: acréscimo aumenta as despesas, redução as diminui, sem
virar receita.

**Rótulos.** O saldo do card é o **saldo do orçamento mensal**: receitas menos
despesas lançadas no mês. Não é saldo efetivo de conta (que só considera efetivados)
nem previsão de caixa; o card avisa isso.

**Decisão em aberto.** A #24 pede fechar como pendentes aparecem nos indicadores
antes de combinar "realizado" e "previsto". Esta entrega não cria essa combinação:
usa a mesma regra dos totais da tela de Lançamentos (todo lançamento do mês, uma
vez). Quando a regra for decidida, ela muda só o `Ledger`.

A janela de próximos sete dias e atrasados é da #58 e não depende deste resumo.
