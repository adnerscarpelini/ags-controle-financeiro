# Faturas por cartão no dashboard

A [#26](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/26) mostra
o comprometimento com cartões (`IInvoiceReports`, em `Reports.Contracts`; seção do
dashboard).

**Fonte.** Reports lê `IInvoiceCatalog` (Invoices), a mesma origem da tela Faturas;
por isso o total de cada fatura é o dessa tela, com os ajustes
([#52](../invoices/total-adjustments.md)) incluídos uma única vez e com pagamentos,
cashback e descontos separados.

**Tabela do mês.** Por cartão: fatura, cashback/desconto, pago, em aberto, mês anterior,
média no ano e em aberto daqui pra frente (o saldo que a tela de obrigações já usa: o
mês selecionado e os meses futuros com parcelas). A linha de total soma cada coluna. A coluna **Previsto** mostra o total das recorrentes previstas que ainda não somam na fatura ([#69](../invoices/recurring-forecast.md)): é zero quando a preferência é "Somar desde já", porque elas já estão no total (#71).
O cartão entra se tem fatura no mês ou saldo daqui pra frente. A média do ano usa os
meses de janeiro até o selecionado em que o cartão tem fatura.

**Gráfico.** Colunas empilhadas, uma por mês de janeiro do ano selecionado até o último
mês com fatura (incluindo meses futuros com parcelas já lançadas), na cor cadastrada de
cada cartão. O cartão entra na série assim que tem fatura, sem alterar código. É desenhado
com HTML/CSS próprios, com legenda e texto alternativo.

**Arquivados.** Cartões e contas arquivados ([#53](../accounts/archiving.md)) continuam no
relatório e no total enquanto têm fatura; marcados como "(arquivado)". O filtro de ativos
das novas seleções não se aplica aqui.

**Logo.** A identificação usa o logo circular do banco (#54) quando existe e, sem ele, as
iniciais do banco, sempre com o nome do cartão em texto. A imagem não altera cálculos.

**Rótulo "Pago".** A coluna soma os pagamentos confirmados da fatura (parciais ou totais,
antes ou depois do fechamento); a issue a chama de "pago antecipado", mas o contrato de
Invoices não distingue a data do pagamento em relação ao fechamento. Previsão de gastos
não lançados está fora do escopo.
