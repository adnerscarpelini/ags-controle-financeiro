# Despesas e receitas por categoria contra a média

A [#25](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/25) mostra
onde o mês saiu do padrão (`ICategoryReports`, em `Reports.Contracts`; seção do
dashboard abaixo do [resumo](monthly-summary.md)).

**Mesma base do resumo.** Usa o `Ledger` da #24, então a soma das linhas de despesa é
igual às despesas do resumo e a das receitas às receitas. O ajuste de fatura
([#52](../invoices/total-adjustments.md)) aparece uma única vez, como a linha própria
"Ajuste de fatura", sem categoria fictícia; uma redução diminui o valor da linha e
nunca vira receita.

**Despesas por categoria.** Da maior para a menor: valor, % do mês, valor do mês
anterior, média no ano e diferença para a média (mês − média). Categorias sem
lançamento no mês não aparecem. As 10 maiores também saem em barras horizontais,
desenhadas com HTML/CSS próprios (sem biblioteca nem CDN).

**Receitas por origem.** Mesma tabela, por "categoria > subcategoria".

**Média no ano.** Soma da categoria de janeiro até o mês selecionado dividida pelo
número de meses desse intervalo com algum lançamento do mesmo tipo (despesa para a
tabela de despesas, receita para a de receitas), a mesma regra da média do resumo.

**Cores.** Despesa acima da média é destacada na cor de despesa e abaixo na de receita;
nas receitas é o inverso. O sinal (+/−) acompanha a diferença, para a cor não ser o
único sinal. Detalhar por subcategoria em outra tela está fora do escopo.
