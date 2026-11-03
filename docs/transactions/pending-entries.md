# Lançamentos pendentes

A [#57](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/57)
acrescenta Pendentes ao menu e QueryPendingAsync aos Contracts de Transactions.
A consulta usa o mesmo EntryFilter, a paginação e os totais da #15, restringindo
os dados a lançamentos pendentes fora do cartão, avulsos ou recorrentes (#88). Totais representam todo o filtro,
não somente a página, e a ordenação é pela data prevista crescente, com desempate estável.

A tela mostra data prevista, descrição, receita/despesa, valor, conta e série.
Atrasado, Hoje e Futuro são textos explícitos. É possível combinar conta, tipo,
intervalo de datas, mês contábil e busca textual. O contrato também aceita os
demais filtros compartilhados. O resumo é previsão, não saldo efetivo da conta.

Sem intervalo futuro, a geração segue os meses até o atual e inclui ocorrências
futuras já conhecidas. Informar data final ou mês futuro materializa os períodos
necessários. A consulta completa não tem uma janela fixa de sete dias.

Pagar ou Receber abre uma confirmação com valor, data e conta editáveis (#88).
Cancelar ou falhar mantém a pendência; confirmar remove da lista e preserva o mesmo
lançamento no histórico, agora Pago ou Recebido, sem duplicação. A conta original
arquivada continua disponível e identificada; outra conta ativa pode ser escolhida.
Desfazer a baixa devolve o lançamento à consulta e restaura seu saldo derivado.
