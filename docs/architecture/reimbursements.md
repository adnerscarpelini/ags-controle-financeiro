# Reembolsos

## Contexto

O reembolso era apurado por regras de rateio: filtro salvo, percentual e mês inicial
(#21/#22), com recebimento avulso por valor (#23). Mudar um filtro alterava meses
passados, e nenhum lançamento indicava de quem era o reembolso.

## Decisão

O reembolso é explícito: pessoa e percentual são informados na própria despesa. As
regras de rateio, a apuração por filtro e o recebimento avulso foram removidos (#78),
sem migração de dados, com autorização para descartar os dados de desenvolvimento.

**Onde fica.** `TransactionEntry` e `RecurringEntry` guardam `ReimbursementPersonExternalId` e
`ReimbursementPercent`, com referência à pessoa por `ExternalId` e sem FK entre schemas.
Assim, parcelas, alcance e duplicação reaproveitam o fluxo dos demais campos, em vez de
serem refeitos em Reimbursements.

**Direção da dependência.** Reimbursements continua consumindo Transactions. Para validar
a pessoa sem ciclo, Transactions define `IReimbursementWriteGuard` em `Contracts`, e
Reimbursements o implementa, como em `IInvoiceWriteGuard`.

**Pendente é derivado.** O item Pendente é calculado na leitura, a partir dos lançamentos
com pessoa, e não é uma linha gravada. Nenhuma edição, série, parcela ou importação precisa
sincronizar outra tabela, então não há como divergir.

**Estado:** cadastro de pessoas (#78), pessoa e percentual em despesas (#79) e recorrentes
(#80) e análise do mês (#81) entregues. Os demais passos estão nas issues
[#82](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/82) a
[#84](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/84).

## Alternativas

- **Manter as regras por filtro:** descartada pelos problemas acima.
- **Gravar um reembolso por despesa:** exigiria sincronizar todas as mutações de
  Transactions, sem mensageria, com risco de divergência.
- **Tabela própria de vínculos em Reimbursements:** evitaria as colunas em Transactions,
  mas obrigaria a reimplementar alcance de parcelas e séries fora do módulo que as conhece.
