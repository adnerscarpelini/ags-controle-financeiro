# Histórico de pagamentos e abatimentos

A [#19](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/19)
introduz InvoicePayment para Pagamento, Cashback e Desconto, com identidade,
confirmação, fatura, data, valor positivo, detalhes e conta debitada quando houver.
A referência à fatura tem FK dentro do schema invoices; cartões e contas continuam
referenciados apenas por ExternalId, sem FK entre módulos.

Cashback e desconto podem ser criados, editados, movidos e excluídos em faturas
Abertas/Fechadas. Em Paga, essas ações são bloqueadas, tanto na origem quanto no
destino de uma edição. Esta política foi aprovada pelo usuário na implementação.
Pagamentos não usam os comandos de abatimento; são desfeitos pela reabertura da #93.

A área Cashback permite registrar os dois tipos de abatimento e consultar o
histórico por cartão e ano de recebimento. O total anual e a discriminação por
cartão somam apenas Cashback, sem incluir descontos ou pagamentos.
A fatura mostra total bruto, cashback, descontos, pagamentos e saldo restante,
com histórico de data, valor, tipo, detalhes e conta efetivamente debitada.
Saldo negativo aparece como crédito com texto e o token Warning.

Abatimentos não geram receita, despesa ou débito em conta; também não mudam a
situação da fatura automaticamente. Saldo zero/crédito continua exigindo quitação
explícita. A confirmação usa transação financeira e identidade estável para não
duplicar o cadastro por clique repetido; falhas revertem também uma nova fatura.

## Pagamento com débito e quitação (#51)

A [#51](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/51)
permite pagar em Aberta/Fechada, inclusive parcial, antecipado ou excedente.
A tela sugere a corrente vinculada ao cartão e permite outra corrente ativa.
Data, valor e conta são apresentados antes da confirmação.
Conta inexistente, arquivada ou que não seja corrente é rejeitada; saldo
insuficiente não bloqueia o débito. A conta escolhida fica gravada no pagamento.

Invoices coordena o histórico, o estado da fatura e o movimento de caixa de
Transactions na mesma operação financeira. Falhar em qualquer gravação reverte
todas. A identidade da confirmação impede um segundo pagamento/débito em chamadas
repetidas ou concorrentes. Pagamentos não criam uma segunda despesa e não oferecem
edição ou exclusão individual; a reabertura desfaz todos os pagamentos da fatura.

Reabrir uma fatura com pagamento confirmado desfaz todos os pagamentos (#93). Pagamento
antecipado mantém a fatura Aberta; saldo zero/crédito não altera a situação sozinho.
Em Fechada, a confirmação de pagamento pode incluir uma escolha explícita de
quitação, aceita somente se o saldo final for zero/crédito. Há também Confirmar
quitação para Fechada com saldo zero/crédito, sem criar pagamento ou débito zerado.
PaidAt registra o instante da quitação. Repetir a confirmação não cria movimento.

## Reabertura com desfazimento (#93)

A [#93](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/93) permite
Reabrir fatura em Paga, Fechada ou Aberta com pagamento confirmado. Incluir Abertas
com pagamento parcial/antecipado foi confirmado pelo usuário. O diálogo lista data,
valor e conta de todos os pagamentos ativos; cancelar não chama a operação.

A confirmação remove os débitos de Transactions e grava UndoneAt nos pagamentos,
sem apagar suas linhas, identificadores, confirmações ou anexos. Cada valor volta à
conta originalmente debitada, mesmo arquivada. A fatura volta a Aberta, sem PaidAt
ou pagamento confirmado, e suprime fechamento automático até fechamento manual.
Cashback e desconto permanecem. Antecipação de parcelas não é desfeita.

O histórico identifica Desfeito e todas as somas, inclusive saldo restante, compromissos
e Reports, ignoram esses pagamentos. Repetir a reabertura não devolve dinheiro duas
vezes; reenviar a confirmação original de um pagamento desfeito não o recria.
Pagamento, movimento de conta e estado da fatura usam a mesma transação: qualquer
falha reverte tudo. Enquanto Paga, compras e ajustes continuam protegidos.

## Janela de pagamento (#100)

**Pagar fatura** abre `InvoicePaymentDialog`, com valor, data e a conta vinculada já selecionada, que pode ser trocada. O segundo passo revisa e avisa que o pagamento só pode ser desfeito reabrindo a fatura. Se o anexo falhar, o pagamento continua registrado e a janela oferece reenviar somente o documento.
