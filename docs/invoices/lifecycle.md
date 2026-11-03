# Situação, fechamento e reabertura

A [#50](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/50)
implementa somente Aberta, Fechada e Paga, separadas do saldo. Fechar não paga a
fatura, mesmo que o total seja zero ou crédito. Quitação explícita e registro de
pagamentos são integrados nas #19/#51; não existe botão genérico para marcar Paga.

## Fechamento

O host verifica faturas de minuto em minuto e na inicialização, respeitando o
fuso local do negócio. IncludesClosingDay=true fecha à meia-noite seguinte ao dia
de fechamento; false, à meia-noite do próprio dia. Dias acima do fim do mês usam
o último dia. O caso de uso também verifica o corte ao validar uma compra,
impedindo que o intervalo entre verificações permita gravar em fatura vencida.

Quando dois fechamentos calculados pelo calendário de Accounts apontam para o
mesmo mês de referência, usa-se o último. Para um mês escolhido manualmente sem ciclo
correspondente, usa-se o último fechamento anterior ao vencimento. Essas regras
foram aprovadas para os limites de fevereiro; não alteram o mês gravado da compra.
Se a meia-noite não existir no fuso, o corte usa o primeiro instante local válido.

Fechar manualmente pede confirmação e também permite registrar uma fatura vazia
por cartão/mês. Isso permite localizar e reabrir manualmente uma fatura histórica
antes de registrar compras nela. Consultas não criam faturas artificiais.

## Proteção e reabertura

Novas compras, parcelas, edição, exclusão, movimentação e mesclagem de descrições
são protegidas pelos casos de uso.
Operações que atinjam uma fatura protegida falham integralmente, sem sucesso
parcial. Recorrências também usam essa proteção (#16); CSV a consumirá nas próximas etapas.
O limite de [operação financeira](../architecture/financial-operations.md)
serializa validação e gravação, sem dependência inversa entre módulos.

Reabrir exige Fechada sem pagamento confirmado. Paga não reabre. A reabertura
manual permanece Aberta até novo fechamento manual, mesmo se o corte já passou;
não recalcula nem move compras. O histórico de pagamentos será integrado nas
#19/#51 à condição de irreversibilidade já preparada no domínio.

O fechamento automático pausa durante a manutenção de restauração. Logs de falha
não incluem dados financeiros. Cancelar um diálogo não chama o caso de uso.

Antes de fechar, o caso de uso materializa os planos recorrentes daquele mês.
Planos cadastrados antes do corte podem completar uma fatura que continua Aberta
após um período desligado; não podem modificar Fechada/Paga. Ver as
[regras de recorrência](../transactions/recurrences.md).
