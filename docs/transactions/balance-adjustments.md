# Ajustes de saldo

A [#48](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/48)
acrescenta Ajustar saldo ao cadastro de conta e à tela Transferências e saldos.
Informam-se conta ativa, saldo correto e data de referência. Antes de confirmar,
a tela apresenta o saldo calculado naquela data, o informado e a diferença.
Cancelar não grava nada; se o saldo mudar após a prévia, é preciso conferi-la novamente.

O caso de uso reutiliza IEntryCatalog.GetBalanceAsync dentro da mesma operação
financeira. A diferença é um AccountMovement de tipo BalanceAdjustment, com a
conta, data e saldo-alvo registrados. Abertura, lançamentos anteriores e ajustes
anteriores permanecem. Saldo correto pode ser negativo. Diferença zero não cria
linha. Repetir a mesma confirmação não duplica o ajuste.

Ajustes retroativos usam o saldo efetivo na data escolhida e influenciam os saldos
das datas seguintes junto dos demais movimentos. A data precisa ser a partir da
referência de abertura: movimentos anteriores a ela são excluídos do saldo pelas
regras da #47. Contas inexistentes/arquivadas são rejeitadas.

Ajustes de saldo não são receita/despesa, não entram nos totais do orçamento e não
exigem estabelecimento fictício. O histórico mostra a diferença e o saldo-alvo.
Corrigir exige novo ajuste; não há edição/exclusão silenciosa do anterior.
Não é ajuste de total de fatura, cuja operação pertence à #52.
