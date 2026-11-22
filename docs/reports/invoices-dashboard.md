# Faturas a pagar no Dashboard

A [#144](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/144) trocou o card
com a fatura de cada cartão no mês escolhido
([#112](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/112)) pelo card
**Faturas a pagar** (`CardInvoicesSummary`), na faixa [Hoje](dashboard.md) do Dashboard. O card
não depende do mês selecionado.

## Quais faturas entram

A seleção é do módulo Invoices, em `IInvoiceCatalog.GetPayableAsync`:

- **Fechadas com saldo restante maior que zero.** Fechada com saldo zero ou crédito fica de fora,
  mesmo sem a quitação confirmada; a confirmação de quitação
  ([#131](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/131)) continua na tela
  de Faturas.
- **De cada cartão, a Aberta atual**: a primeira Aberta a partir do mês calculado para uma compra
  feita hoje, a mesma fatura que o sistema sugere ao lançar uma compra
  ([mês de referência da compra](../accounts/purchase-reference-month.md)).
- **Abertas anteriores à atual**, por exemplo uma fatura reaberta
  ([#93](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/93)) ou uma fatura cujo
  corte já passou, mas que ainda não foi fechada automaticamente.
- Abertas só entram com gasto ou abatimento.

Ficam de fora as Pagas e as Abertas futuras (que só têm parcelas). A ordem é pelo vencimento e,
no mesmo vencimento, pelo nome do cartão. Um cartão pode aparecer duas vezes, por exemplo com uma
Fechada a pagar e a Aberta atual.

A situação pelo calendário e a primeira Aberta (`CalendarStatus` e `FirstOpenMonth`, em
`InvoiceCatalog.Selection.cs`) são as mesmas da seleção de fatura no lançamento, para as duas
telas nunca discordarem sobre qual é a fatura atual. As recorrentes do mês da fatura atual são
geradas antes de somar.

## Cada linha

- Nome do cartão, com link para a fatura, e a situação: **Aberta** ou **Fechada**.
- **Fecha em dd/MM**, só para a Aberta com fechamento hoje ou depois. Depois do corte e antes do
  fechamento automático, a data já passou e não é anunciada.
- **Vence em dd/MM/yyyy** ou, se o vencimento já passou, **Venceu em dd/MM/yyyy** na cor de despesa.
- Em destaque, o que falta pagar (`RemainingBalance`, cor de `ValueColor.Owed`). Abaixo,
  "Total R$ X · abatido R$ Y", em que o abatido soma pagamentos, antecipações, cashback e
  descontos; sem abatimento, só "Total R$ X".
- Cartão arquivado ([#53](../accounts/archiving.md)) com fatura a pagar continua aparecendo,
  marcado "(arquivado)".

O total no topo do card soma os valores restantes. Sem faturas a pagar, o card mostra "Nenhuma
fatura a pagar." e total R$ 0,00. O link "Ver cartões" leva à carteira.

## Total da fatura

O total é o da tela de faturas, com os ajustes ([#52](../invoices/total-adjustments.md)) uma
única vez, e respeita a preferência de recorrentes do cartão
([somar só na data ou desde já](../invoices/recurring-forecast.md)). O card não mostra mais a linha
"Previsto" nem o limite usado do cartão.

## Fora do card

O detalhe de cada fatura, com pagamentos e compras, fica na tela da fatura. O saldo das parcelas
futuras e o limite ficam em Cartões.

## Como verificar

`InvoiceTests.Payable` (Invoices) cobre Fechada com saldo, Fechada zerada ou com crédito, Paga,
Aberta atual, Aberta anterior e Abertas futuras; `DashboardSummariesTests` (Web) cobre a linha,
datas, arquivado, estado vazio e total.
