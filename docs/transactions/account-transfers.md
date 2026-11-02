# Transferências e movimentos de caixa

A [#46](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/46)
acrescenta Transferências e saldos ao menu. A origem precisa ser corrente ativa;
o destino, investimento ou poupança ativa. São exigidos contas distintas, data e
valor positivo. A confirmação mostra os dados; cancelar não grava nada.
Saldo insuficiente não bloqueia a operação.

AccountMovement guarda os dois lados, com valores opostos e a mesma identificação
de operação. IAccountMovementCatalog consulta Accounts somente pelos Contracts.
A gravação conjunta usa a operação financeira transacional existente, sem FK ou
SQL entre schemas. Falhar ao gravar a entrada também desfaz a saída.

A confirmação tem identidade estável; repeti-la com os mesmos dados não duplica
movimentos, inclusive em chamadas concorrentes. Reutilizar a identidade com outros
dados falha. Histórico mantém os dois movimentos mesmo se as contas forem depois
arquivadas. Transferências confirmadas não oferecem edição/exclusão nesta entrega.

O saldo efetivo soma os movimentos de caixa e os lançamentos fora do cartão já
efetivados, respeitando a referência de abertura. Consultas por data preservam o
saldo anterior à transferência. AccountMovement não entra na grade de receitas e
despesas: não se cria descrição, categoria ou receita fictícia para representar
uma entrada entre contas do mesmo patrimônio.
