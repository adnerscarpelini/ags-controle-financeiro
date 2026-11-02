# Compras no cartão

A [#12](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/12)
acrescenta cartão e final opcional à entrada rápida. Accounts sugere o mês pelo
calendário do cartão; mudar data ou cartão recalcula a sugestão. O usuário pode
escolher outra fatura antes de salvar, preservando a data da compra.

O caso de uso exige cartão disponível, final ativo pertencente a ele e classificação
de despesa. A conta gravada vem do cartão; não é substituída por uma conta divergente
enviada no formulário. Os vínculos são ExternalId, sem consultas cruzadas de schemas.

As despesas de cartão aparecem no orçamento e nas faturas, mas não movimentam
diretamente o saldo efetivo da conta. O débito pertence ao pagamento de fatura (#51).
A proteção de faturas Fechadas/Pagas e do horário de corte está integrada na
[#50](../invoices/lifecycle.md), inclusive para a escolha manual de outra fatura.
