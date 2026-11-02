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
usam operações rastreáveis da #48, sem substituir a abertura.

## Integração de caixa implementada na #11

Transactions já pode consumir Contracts de Accounts. O cálculo de saldo efetivo
usa a abertura e somente movimentos efetivados a partir da referência, inclusive
o próprio dia. Movimentos anteriores continuam no histórico e no orçamento, mas
não são somados novamente ao saldo de abertura (#11). Antes da referência,
a abertura ainda não compõe o saldo consultado. Não há dependência inversa,
FK ou consulta cruzada de tabelas, nem saldo mutável duplicado em Accounts.
A #56 integra ocorrências pendentes e baixa manual. A confirmação grava o estado,
a data efetiva e o instante na própria ocorrência. O saldo deriva desses dados,
sem uma segunda escrita de valor acumulado. A operação financeira serializa
confirmações concorrentes; repetir retorna a confirmação original.
O orçamento continua usando Month, enquanto o saldo usa EffectiveDate.
Edições/exclusões autorizadas recalculam o saldo derivado.

Alternativa descartada: registrar uma receita inicial confundiria patrimônio
prévio e receitas do período. Consequência: consultas de caixa devem consumir
explicitamente a abertura, enquanto consultas de orçamento usam os lançamentos.

## Movimentos sem orçamento (#46)

Transferências usam AccountMovement em Transactions, separados das receitas e
despesas. IEntryCatalog soma também esses movimentos no saldo efetivo, com a mesma
regra de data e referência de abertura. Não há segundo cálculo nem saldo gravado
em Accounts. Isso permite preservar o patrimônio entre contas e manter os totais
do orçamento sem duplicidade. Ambos os lados usam a mesma operação financeira.

## Ajustes rastreáveis (#48)

Ajustar saldo calcula a diferença pelo mesmo IEntryCatalog, dentro da transação.
AccountMovement conserva o saldo-alvo e a diferença, sem alterar a abertura.
A configuração de prévia permite rejeitar um saldo que mudou antes da confirmação.
O histórico de ajustes é preservado; correções são novos movimentos.
