# Planejamento de receitas e despesas

A [#77](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/77) mostra numa só tabela onde
o dinheiro foi gasto e o que vem pela frente (`IPlanningReports`, em `Reports.Contracts`; página
`/planejamento`).

**Período.** 19 meses de referência: 6 anteriores, o atual e 12 seguintes. As setas deslocam o período um
mês por vez, mantendo as 19 colunas. O mês atual fica em destaque.

**Linhas.** Receitas, despesas, saldo do mês e saldo acumulado; abaixo, receitas e despesas por categoria,
fechadas por padrão, que abrem em subcategorias. Dentro de cada tipo, a ordem é do maior para o menor
total do período. O ajuste de fatura ([#52](../invoices/total-adjustments.md)) é uma linha própria, sem
subcategorias.

**O que entra.** Lançamentos efetivados e pendentes, parcelas, compras em faturas futuras, ajustes de
fatura uma única vez e recorrentes. Transferências e pagamentos de fatura ficam de fora. Até o mês atual,
receitas e despesas são iguais às do [resumo](monthly-summary.md). A soma das categorias de cada mês é igual
às linhas de total (coberto por teste).

**Recorrentes de meses futuros.** Meses posteriores ao atual são calculados sem gravar nada: abrir a tela não
cria lançamento nem fatura, então não muda outras telas. O cálculo é o mesmo da geração
(`RecurringOccurrencePlanner`), e respeita término, meses excluídos, ocorrências já gravadas (somadas uma vez,
com o valor gravado) e faturas protegidas. Por isso `IInvoiceWriteGuard` ganhou `CheckRecurringAsync`, que decide
como `RequireRecurringAsync`, mas não registra a fatura que ainda não existe. Até o mês atual vale a regra dos
demais relatórios, que garante as ocorrências até o mês consultado.

**Previsto.** A célula mostra o total e fica em itálico e sublinhada quando inclui valor previsto. Previsto é:

- recorrente pendente fora do cartão, inclusive atrasada, até a baixa manual
  ([#56](../transactions/pending-entries.md));
- recorrente do cartão com data futura em fatura Aberta ([#69](../invoices/recurring-forecast.md));
- ocorrência calculada de um mês posterior ao atual, ainda sem registro.

Parcelas e compras avulsas contam como lançadas, mesmo com data futura. A célula marcada é um botão: passar
o mouse mostra o detalhe "lançado + previsto" e tocar ou usar o teclado o escreve acima da tabela. A preferência
"Recorrentes do cartão na fatura" não muda esta tela: como as demais visões de orçamento, ela conta a
ocorrência desde já.

**Saldo acumulado.** Começa em zero e soma os saldos do mês atual em diante, mesmo quando o mês atual está fora do
período exibido; meses anteriores mostram `-`. Não usa o saldo das contas, e o rótulo avisa isso.

**Cores.** Na tabela de categorias, a intensidade é o valor dividido pelo maior valor de categoria do mesmo tipo
no período, e as subcategorias usam a mesma escala. É diferente do [mapa de calor](heat-map.md), que compara
cada linha com ela mesma. A cor só reforça o valor, que sempre aparece em texto.

**Interface.** No celular, a coluna de nomes fica fixa e os meses rolam na horizontal. [Ocultar
valores](../web/hide-values.md) cobre o texto das células e o detalhe, inclusive `title` e `aria-label`.

**Falha na projeção.** Se uma recorrente futura não puder ser calculada (por exemplo, a fatura calculada excede o
intervalo de datas suportado), a tela avisa e mostra o que está gravado.

Clicar num valor para abrir os lançamentos, exportar, previsão de caixa por conta e filtros estão fora do escopo.

Testes: `PlanningTests` (regras) e `PlanningPageTests` (tela).
