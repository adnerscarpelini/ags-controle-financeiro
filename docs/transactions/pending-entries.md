# Lançamentos pendentes

A [#57](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/57)
acrescenta Pendentes ao menu e QueryPendingAsync aos Contracts de Transactions.
A consulta usa o mesmo EntryFilter, a paginação e os totais da #15, restringindo
os dados a recorrências pendentes fora do cartão. Totais representam todo o filtro,
não somente a página, e a ordenação é pela data prevista crescente, com desempate estável.

A tela mostra data prevista, descrição, receita/despesa, valor, conta e série.
Atrasado, Hoje e Futuro são textos explícitos. É possível combinar conta, tipo,
intervalo de datas, mês contábil e busca textual. O contrato também aceita os
demais filtros compartilhados. O resumo é previsão, não saldo efetivo da conta.

Sem intervalo futuro, a geração segue os meses até o atual e inclui ocorrências
futuras já conhecidas. Informar data final ou mês futuro materializa os períodos
necessários. A consulta completa não tem uma janela fixa de sete dias.

Registrar baixa manual reutiliza a #56. Cancelar ou falhar mantém a pendência;
confirmar remove da lista e preserva o mesmo lançamento no histórico, agora
efetivado, sem duplicação. Contas arquivadas continuam disponíveis no filtro e
na lista, identificadas por texto. O diálogo avisa que a baixa será registrada
na conta original arquivada, sem mudar o vínculo silenciosamente.
