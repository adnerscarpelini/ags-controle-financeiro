# Saldos mensais e parcelas futuras

A [#20](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/20)
acrescenta Saldos de faturas e parcelas futuras, acessível pela página Faturas.
IInvoiceCatalog.GetObligationsAsync expõe a mesma consulta por Contracts.

Por mês, o total soma o saldo restante de todos os cartões, já descontados
pagamentos, cashback e descontos. O mês corrente tem identificação textual.
Um mês com todas as faturas Pagas e saldo zerado aparece como - (pago).
Crédito continua visível e destacado, mesmo após quitação; zero sem confirmação
não é rotulado como Paga.

Para cada cartão, Em aberto daqui para frente soma a fatura do mês selecionado
e as faturas posteriores que contêm parcelas lançadas. Usa o saldo restante da
fatura inteira, incluindo abatimentos, sem somar novamente o total bruto.
Não projeta compras ainda não lançadas. Contas/cartões arquivados continuam
incluídos; disponibilidade cadastral não apaga obrigações financeiras.
