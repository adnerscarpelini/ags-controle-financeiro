# Histórico de pagamentos e abatimentos

A [#19](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/19)
introduz InvoicePayment para Pagamento, Cashback e Desconto, com identidade,
confirmação, fatura, data, valor positivo, detalhes e conta debitada quando houver.
A referência à fatura tem FK dentro do schema invoices; cartões e contas continuam
referenciados apenas por ExternalId, sem FK entre módulos.

Cashback e desconto podem ser criados, editados, movidos e excluídos em faturas
Abertas/Fechadas. Em Paga, essas ações são bloqueadas, tanto na origem quanto no
destino de uma edição. Esta política foi aprovada pelo usuário na implementação.
Pagamentos confirmados são irreversíveis e não usam os comandos de abatimento.

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

A estrutura e a consulta de pagamentos estão preparadas nesta etapa. A confirmação
de Pagamento será disponibilizada pela #51, junto da escolha de conta, débito
atômico e quitação explícita. Não existe uma confirmação monetária intermediária
que possa registrar pagamento sem o débito correspondente.
