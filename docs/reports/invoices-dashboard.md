# Faturas no dashboard

A [#112](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/112) substituiu a
seção da [#26](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/26) (tabela
por cartão e gráfico por mês) por um card com a fatura de cada cartão no mês escolhido
(`CardInvoicesSummary`).

**Fonte.** O card lê `IInvoiceCatalog` e `IAccountCatalog` pelos contratos. O total é o da tela
de faturas, com os ajustes ([#52](../invoices/total-adjustments.md)) incluídos uma única vez.

**Cada cartão.** Valor em destaque: o que falta pagar (`RemainingBalance`). Abaixo: total gasto e
abatimentos (pagamentos, antecipações, cashback e descontos), vencimento, situação e limite usado.
Sem valor no mês, mostra "Fatura zerada". O rodapé soma o total a pagar.

**Previsto.** Quando as recorrentes futuras ficam fora do total, conforme a preferência, o card
mostra o valor previsto à parte ([recorrentes previstas](../invoices/recurring-forecast.md)).

**Arquivados.** Cartões arquivados ([#53](../accounts/archiving.md)) continuam no card enquanto
têm fatura no mês, marcados como "(arquivado)".

**Fora do Dashboard.** O detalhe de cada fatura, com pagamentos e compras, fica na tela da fatura.
O saldo das parcelas futuras fica em Cartões, em "Saldos de faturas e parcelas futuras".
