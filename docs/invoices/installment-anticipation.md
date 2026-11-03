# Antecipação de parcelas

A [#32](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/32)
oferece Antecipar parcelas na linha de uma parcela, com quantidade e desconto
opcional. O calendário de Accounts calcula a fatura atual no fuso do negócio.
A confirmação mostra destino, quantidade e desconto; cancelar não altera dados.

O caso de uso pertence a Invoices e consome somente Contracts de Transactions.
Seleciona as últimas parcelas que ainda estão em meses posteriores ao atual e
move apenas o mês contábil, preservando valores, datas, identidade e numeração.
O desconto, quando positivo, é InvoicePayment do tipo Desconto, com a compra
identificada nos detalhes. Sem desconto não há abatimento de valor zero.
Quantidade excedente e desconto maior ou igual ao total antecipado são rejeitados.

Todas as origens e o destino respeitam a proteção de faturas. Movimentos,
abatimento e registro da confirmação usam a mesma transação financeira: uma
falha ou fatura protegida reverte tudo, mesmo se outra parcela já tiver sido
validada. Uma confirmação estável impede repetir a antecipação em chamadas
concorrentes. Se o mês atual mudar durante o diálogo, é preciso conferir novamente.
Não há operação de desfazer antecipação nesta entrega.
