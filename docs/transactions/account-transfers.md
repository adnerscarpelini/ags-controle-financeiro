# Transferências e movimentos de caixa

A [#46](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/46)
acrescenta Transferências e saldos ao menu. A origem precisa ser corrente ativa;
o destino, qualquer outra conta ativa, inclusive outra corrente
([#124](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/124)). São exigidos contas distintas, data e
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

## Janela de transferência (#97)

**+ Novo > Transferência** abre `Components/Entries/TransferDialog.razor`, com valor, data, conta de origem e conta de destino. Salvar continua pedindo a confirmação com contas, data e valor antes de movimentar as contas.

## Tela de Contas (#99)

Na [tela de Contas](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/99), `/contas`, o botão **Transferir** abre a janela, e o menu ⋮ de uma conta traz **Transferências**, com o histórico de movimentos da conta e a opção de nova transferência a partir dela. `/transferencias` redireciona para `/contas`.

## Transferência recorrente (#124)

A [#124](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/124) acrescenta **Repetir todo mês** à janela, para casos como um PIX automático entre contas. O dia vem da data, com término opcional; em meses curtos, a ocorrência cai no último dia sem mudar o dia da série.

`RecurringTransfer` guarda o plano, e cada mês vira uma `TransferOccurrence`. Como nas despesas, a primeira segue a chave **Transferido**, ligada por padrão, e as seguintes nascem pendentes. Passar a data só torna a pendência atrasada. Os meses são gerados nas consultas, até o mês atual ou o mês consultado.

- **Confirmar** permite ajustar valor e data só daquele mês e cria os dois `AccountMovement` uma única vez. Confirmar de novo não muda nada.
- **Desfazer** remove os movimentos e deixa o mês pendente.
- **Editar** pergunta o alcance: Somente esta ou Esta e as futuras em aberto. Só o que mudou se propaga. Mês confirmado precisa ser desfeito antes.
- **Excluir** pergunta o alcance: Somente esta, que não recria o mês, Esta e as seguintes ou Todas. Meses confirmados continuam no histórico.

A tela de Contas lista os meses da série. Pendentes entram no saldo previsto das duas contas e na lista de pendências do Dashboard, fora dos totais a receber e a pagar. Transferência continua fora de receitas, despesas e orçamento, e não aparece em Lançamentos nem no CSV.
