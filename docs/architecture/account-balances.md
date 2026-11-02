# Abertura e responsabilidade pelo saldo

## Decisão implementada na #47

[Saldo inicial](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/47)
é metadado da conta em Accounts: Money em centavos e data de referência. Ambos
são gravados junto do cadastro, sem simular receita. Saldo negativo é permitido.
A data é obrigatória ao informar o saldo; o formulário sugere hoje no fuso local
de negócio, obtido de TimeProvider. Uma nova corrente sem valor informado começa
com zero e referência de hoje. Contas anteriores à migration mantêm zero sem
referência até sua configuração, preservando os dados existentes.

A abertura registrada é preservada ao editar o cadastro; correções de saldo
serão operações rastreáveis da #48, sem substituir a abertura.

## Integração aprovada, a entregar nas próximas issues

Transactions já pode consumir Contracts de Accounts. O cálculo de saldo efetivo
usará a abertura e somente movimentos efetivados a partir da referência, inclusive
o próprio dia. Movimentos anteriores continuam no histórico e no orçamento, mas
não serão somados novamente ao saldo de abertura (#11/#56). Antes da referência,
a abertura ainda não compõe o saldo consultado. Não haverá dependência inversa,
FK ou consulta cruzada de tabelas, nem saldo mutável duplicado em Accounts.

Alternativa descartada: registrar uma receita inicial confundiria patrimônio
prévio e receitas do período. Consequência: consultas de caixa devem consumir
explicitamente a abertura, enquanto consultas de orçamento usam os lançamentos.
