# Dashboard: faixas Hoje e Mês

A [#143](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/143) dividiu o
Dashboard (`/dashboard`, `Components/Pages/Dashboard.razor`) em duas faixas, para separar a
posição atual do que acontece num mês escolhido. Cada seção carrega os próprios dados pelos
contratos dos módulos; o Dashboard não calcula valores.

## Faixa Hoje

Não depende de mês: mostra a situação na data de hoje (`TimeProvider`, fuso de negócio), escrita
abaixo do título ("Sua situação em dd/MM/yyyy").

- **Saldo nas contas correntes** (`CheckingAccountsSummary`): o saldo atual de cada conta
  corrente ativa e o total em destaque, o mesmo saldo atual da tela de Contas
  (`IEntryCatalog.GetBalanceForecastAsync`, só a parte atual). Poupança e investimento não
  aparecem nem entram no total. Não mostra saldo previsto.
- **Faturas a pagar** (`CardInvoicesSummary`): ver [Faturas a pagar no Dashboard](invoices-dashboard.md) (#144).
- **Próximos N dias** e **Atrasados**: sem mudança; ver [Pendências do dashboard](pending-agenda.md).

## Faixa Mês

Tem o seletor de mês, que abre no mês atual e só afeta esta faixa.

- **Totais do mês** (`MonthTotals`): Receitas e A pagar no mês vêm do
  [orçamento do mês](monthly-summary.md) (#24, pelo mês de referência); Gasto no mês vem do
  [comportamento do mês](monthly-behavior.md) (#117, pela data da despesa). Os cálculos não mudaram.
- **Despesas por categoria** e **Receitas por origem**: sem mudança, inclusive as colunas de mês
  anterior, média no ano e diferença; ver [Despesas e receitas por categoria](category-report.md).
  O usuário avaliou que essas tabelas funcionam bem, por isso as comparações com o passado
  continuam nelas.

## O que saiu do Dashboard

Os cards de comportamento (mês anterior e média no ano), os cards de orçamento (saldo do
orçamento, % guardado, despesas do mês anterior e média), o saldo previsto das contas, o limite
do cartão e a linha "Previsto" das recorrentes no card de faturas. Os campos sem uso saíram dos
contratos: `MonthlySummary` ficou com `Month`, `Income` e `Expenses`, e `MonthlyBehavior` com
`Month` e `Spent`. O saldo previsto continua em Contas e no rodapé de Lançamentos; o limite, em
Cartões; o planejamento do mês, em `/planejamento`.

## Seções visíveis

A [preferência](../preferences/settings.md) mantém as quatro chaves de antes, com rótulos novos:

| Chave | Rótulo | O que controla |
| --- | --- | --- |
| Summary | Contas e totais do mês | Saldo nas contas correntes (Hoje) e totais do mês (Mês) |
| Pending | Pendências | Próximos N dias e Atrasados (Hoje) |
| Categories | Categorias | Despesas por categoria e Receitas por origem (Mês) |
| Invoices | Faturas a pagar | Faturas a pagar (Hoje) |

A faixa Hoje aparece quando Summary, Invoices ou Pending está ligada; a faixa Mês, quando Summary
ou Categories está ligada. Com tudo desligado, aparece o aviso com link para `/configuracoes`.

Quando uma janela grava algo, as seções recarregam e o mês escolhido é mantido
([#122](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/122)).

## Como verificar

`DashboardSummariesTests` (Web) cobre as faixas (o seletor move só a faixa Mês), os indicadores
removidos, o saldo das contas correntes, as faturas a pagar e a recarga mantendo o mês;
`PreferencesIntegrationTests` (Web) cobre as seções visíveis; `MonthlySummaryTests` e `MonthlyBehaviorTests` (Reports) cobrem os
totais do mês; `InvoiceTests.Payable` (Invoices) cobre a seleção das faturas a pagar.
