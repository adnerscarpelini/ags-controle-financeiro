# Reembolsos do mês

A [#81](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/81) mostra em
`/reembolsos` o que há a receber (`IReimbursementOverviews`, em `Contracts`).

**Filtros.** Mês de referência (padrão: mês atual), pessoa e situação. "Todos os meses" permite
encontrar pendências antigas. Um aviso mostra o total pendente de meses anteriores.

**Item.** Corresponde a uma despesa com pessoa. O valor esperado é o valor da despesa ×
percentual, arredondado ao centavo item a item; o total é a soma dos itens arredondados.
Item com esperado R$ 0,00 não aparece.

**Grade ([#133](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/133)).** Mostra despesa, pessoa, valor integral da conta, percentual, valor a receber,
recebido e situação. O valor da conta fica numa coluna própria, sem sinal e em cor neutra,
para a pessoa conferir com as próprias anotações. No item recebido, ele é o valor gravado no
recebimento.

**Entrada no mês.** A despesa entra pelo mês de referência, Efetivada ou Pendente; a despesa
ainda pendente é indicada no item. Escolher um mês gera as ocorrências recorrentes dele.

**Pendente.** Reflete sempre o lançamento: editar valor, pessoa ou percentual atualiza o
esperado, e excluir a despesa remove o item. O motivo está em
[Reembolsos](../architecture/reimbursements.md).

Reembolsos no dashboard e nos relatórios e cobrança à pessoa estão fora do escopo.
