# Recorrentes previstas na fatura

A [#69](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/69) deixa o usuário escolher
quando uma ocorrência recorrente do cartão passa a somar no total da fatura **Aberta**. O banco só soma
o valor na data da cobrança; somar antes faz o total do aplicativo divergir do que o banco mostra.

## Regra

Uma ocorrência é **prevista** quando pertence a uma série recorrente do cartão e sua data é posterior a
hoje, no fuso de negócio, numa fatura Aberta. No próprio dia da ocorrência ela deixa de ser prevista e
soma sozinha, sem baixa manual. Parcelas e compras avulsas com data futura nunca são previstas.

Exemplo com hoje em 10/11, R$ 100,00 em compras e uma assinatura de R$ 50,00 no dia 20:

| Preferência | Total da fatura | Informação adicional |
| --- | --- | --- |
| Somar só na data (padrão) | R$ 100,00 | Previsto: + R$ 50,00 (total previsto: R$ 150,00) |
| Somar desde já | R$ 150,00 | Inclui R$ 50,00 em recorrentes previstas |

- A preferência muda o cálculo apresentado, não os dados: as ocorrências continuam gravadas como antes e
  trocar a opção altera os valores na hora, sem migrar nada.
- Fatura Fechada ou Paga não tem previstas: o total inclui todas as ocorrências.
- `InvoiceDetails` expõe `ForecastEntries`, `ForecastTotal`, `ForecastIncluded`, `ProjectedTotal` (com as
  previstas, venham somadas ou à parte) e `LaunchedTotal` (sem elas). `Total` e `RemainingBalance` já
  seguem a preferência.

## O que acompanha a preferência

Total e saldo em `/faturas`, valor sugerido em "Pagar", saldos de `/compromissos` e os valores de fatura
do dashboard. A composição da fatura mostra as previstas num grupo próprio, nas duas
opções. A confirmação de fechamento mostra o total com as previstas, porque fechar protege todas elas.

O card de faturas do dashboard mostra **Previsto** (#112) com `ForecastTotal` quando as previstas ficam fora do total; quando já estão somadas, a linha não aparece.

## O que não muda

- Quitação, bloqueios de fatura, pagamentos e demais regras da [#50](lifecycle.md) e da
  [#51](payments.md). A quitação usa o saldo completo da fatura Fechada.
- Despesas do mês, categorias e demais visões de orçamento continuam contando a ocorrência desde já, nas
  duas opções: orçamento mensal e fatura são conceitos distintos.
- `/lancamentos` e o CSV.

## Ajuste de total

Em fatura **Aberta**, o total informado vem do banco, que ainda não cobrou as previstas. Por isso o
[ajuste de total](total-adjustments.md) compara o valor informado só com o **lançado até hoje**
(`LaunchedTotal`), nas duas opções; antes disso, informar o total do banco criava uma diferença falsa.
`InvoiceAdjustmentRequest.RecurringCutoff` leva a data limite de Invoices para Transactions: ocorrências
recorrentes depois dela ficam fora da comparação. Em fatura Fechada a comparação usa o total completo.
A prévia mostra o total usado na comparação.

## Testes

`InvoiceTests.RecurringForecast.cs` (modos, virada de data no fuso de negócio, Fechada, fatura só com
previstas, saldos, ajuste), e `InvoiceForecastPageTests` (tela).
