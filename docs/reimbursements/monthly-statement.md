# Valor a receber de uma pessoa no mês

A [#22](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/22)
apura, para uma pessoa e um mês contábil, o que ela deve ao usuário
(`IReimbursementStatements`, em `Contracts`).

**Cálculo.** Para cada regra vigente da pessoa (mês inicial até o mês consultado) o
filtro salvo ([#33](../transactions/saved-filters.md)) é executado sobre o mês, com
paginação interna, usando os critérios atuais. Só entram despesas; cada item mostra o
total do lançamento, o percentual e o valor devido (total × percentual, arredondado ao
centavo). O total devido é a soma dos valores devidos dos itens.

**Regras de vigência e sobreposição.**
- Um lançamento que cai em duas regras da mesma pessoa é contado uma única vez, pela
  regra de maior percentual.
- Para a mesma pessoa e o mesmo filtro, vale a regra de mês inicial mais recente que
  já começou (uma regra nova substitui a anterior em vez de somar). A issue só define
  "vigente a partir do mês"; esta leitura evita que um percentual antigo mais alto
  continue valendo depois de a regra ser revista.
- Uma regra com mês inicial posterior ao mês consultado não participa.

**Recebido e saldo.** Os recebimentos (`receipts`, registrados pela #23) do mês
abatem o devido: saldo = devido − recebido. Saldo negativo é crédito da pessoa
(`HasCredit`). Recebimentos não são distribuídos entre meses.

**Persistência.** `reimbursements.receipts` guarda pessoa, mês contábil, data, valor,
conta e o `ExternalId` da receita em Transactions (único), sem FK entre schemas.

A tela `/reembolsos` escolhe pessoa e mês e lista itens, total devido, recebido e
saldo. Cobrança ou envio de mensagem à pessoa estão fora do escopo.
