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

## Seleção de fatura (#89)

O formulário mostra Fatura somente com cartão. A consulta inclui meses futuros
sem registro, situações e a próxima disponível após a calculada protegida. A tela
avisa a substituição e permite carregar mais meses até o limite do calendário.
Faturas protegidas não podem ser selecionadas nem enviadas diretamente ao caso de uso.
A proteção considera o corte mesmo quando a rotina de fechamento ainda não gravou
um estado. O cálculo usa o calendário de Accounts, inclusive IncludesClosingDay.

Edições sem mudar data ou cartão conservam a fatura escolhida. Mudar esses dados
recalcula; a escolha explícita de uma fatura após esse recálculo usa o contrato,
sem regra financeira no componente. Em parcelas, a escolha é a primeira fatura e
as seguintes são consecutivas. A antecipação registra os identificadores das parcelas
movidas: detalhes podem mudar, mas data/cartão ficam protegidos. Registros anteriores
à nova coluna não têm esses identificadores; dados legados não são corrigidos nesta issue.

## Janela de despesa no cartão (#97)

**+ Novo > Despesa cartão** abre a [janela de lançamento](cash-entries.md#janela-de-lançamento-97) com cartão e fatura no lugar de conta e Pago. A fatura aparece em opções com mês e situação: as protegidas ficam indisponíveis, e "Carregar próximas faturas" traz mais meses. Em **Mais detalhes** ficam Parcelado, Despesa fixa, final do cartão, reembolso, observação e anexo.

Na edição, o campo Cartão também oferece "Sem cartão", como antes.
