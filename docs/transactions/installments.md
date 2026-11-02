# Compras parceladas

A [#13](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/13)
acrescenta de 2 a 48 parcelas à entrada de compra no cartão. Informe o valor total
e o mês da primeira parcela; 1 no formulário continua sendo uma compra avulsa.

Cada compra cria um InstallmentPurchase e N lançamentos, com relação explícita,
numeração x/N e meses contábeis consecutivos. A data original da compra permanece.
Money.Split calcula as partes e põe a diferença de centavos na primeira, garantindo
que a soma seja exatamente o total. Nenhuma descrição textual é interpretada para
reconstruir parcelas. O vínculo próprio também é mostrado na composição da fatura.

Todas as faturas de destino são validadas pelo guard da #50 dentro da operação
financeira. Compra, parcelas e registros de fatura são confirmados juntos ou
desfeitos juntos; não há gravação parcial quando um mês estiver protegido.
Parcelamento fora do cartão, quantidade inválida e meses além de 12/9999 são
rejeitados. A compra parcelada não debita diretamente o saldo da conta.
