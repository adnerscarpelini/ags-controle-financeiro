# Despesas e receitas por categoria contra a média

A [#25](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/25) mostra
onde o mês saiu do padrão (`ICategoryReports`, em `Reports.Contracts`; seção do
dashboard).

**Base.** As despesas mostram onde se gastou no mês, pela data da despesa, e a soma
das linhas é igual ao gasto do [comportamento do mês](monthly-behavior.md). Quem quer
ver o que caiu numa fatura abre a fatura do cartão. As receitas seguem o mês de
referência, e a soma é igual às receitas do [orçamento](monthly-summary.md). O ajuste
de fatura ([#52](../invoices/total-adjustments.md)) aparece uma única vez, como a linha
própria "Ajuste de fatura", no mês de referência dele, sem categoria fictícia; uma
redução diminui o valor da linha e nunca vira receita.

**Despesas por categoria.** Da maior para a menor: valor, % do mês, valor do mês
anterior, média no ano e diferença para a média (mês − média). Categorias sem
lançamento no mês não aparecem. As 10 maiores também saem em barras horizontais,
desenhadas com HTML/CSS próprios (sem biblioteca nem CDN).

**Receitas por origem.** Mesma tabela, por "categoria > subcategoria".

**Média no ano.** Soma da categoria de janeiro até o mês selecionado dividida pelo
número de meses desse intervalo com algum lançamento do mesmo tipo (despesa para a
tabela de despesas, receita para a de receitas), a mesma regra das médias do dashboard.
Nas despesas, esses meses também são os da data.

**Cores.** Despesa acima da média é destacada na cor de despesa e abaixo na de receita;
nas receitas é o inverso. O sinal (+/−) acompanha a diferença, para a cor não ser o
único sinal. Detalhar por subcategoria em outra tela está fora do escopo.
