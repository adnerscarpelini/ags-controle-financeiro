# Mapa de calor de despesas

A [#27](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/27) mostra
a sazonalidade do ano do mês selecionado (`IHeatMapReports`, em `Reports.Contracts`;
seção do dashboard).

**Linhas do ano.** Receitas, despesas e saldo dos 12 meses (janeiro a dezembro), com a
mesma base do [resumo](monthly-summary.md) e da [tabela por categoria](category-report.md):
efetivados e pendentes uma vez, transferências fora, ajustes de fatura
([#52](../invoices/total-adjustments.md)) uma única vez.

**Categorias × meses.** Categorias de despesa da maior para a menor (total do ano), com
as subcategorias abertas sob cada uma, também da maior para a menor. O ajuste de fatura
é uma linha própria. A soma das linhas de categoria de cada mês é igual às despesas
desse mês no resumo (coberto por teste).

**Calor.** Cada célula traz o valor e uma intensidade de 0 a 1: o valor dividido pelo maior
valor da própria linha. O mês de maior gasto de uma linha tem intensidade 1. A cor é só
reforço, já que o valor sempre aparece em texto. A coluna do mês selecionado fica em
negrito.

**Troca de ano.** O ano vem do mês selecionado, então mudar de ano mostra os meses do novo
ano sem configuração. Ocorrências recorrentes só são geradas até o mês selecionado; os
meses seguintes mostram o que já está lançado (por exemplo parcelas). Exportar a tabela
está fora do escopo. Para os meses futuros com as recorrentes, use o [planejamento](planning.md).
