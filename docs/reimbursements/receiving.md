# Recebimento de reembolsos

A [#82](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/82) efetiva o
recebimento em `/reembolsos` (`IReimbursementReceipts`, em `Contracts`).

**Efetivação.** O usuário seleciona itens Pendentes, de uma ou mais pessoas e meses, informa a
data, a conta corrente ativa e o valor recebido de cada item (padrão: o esperado). O valor pode
ser menor, igual ou maior que o esperado, nunca negativo.

**Situação.** Depois de efetivado, o item fica Recebido e encerrado. A diferença fica visível
pelos dois valores e não vira saldo nem pendência.

**Receita.** Cada efetivação cria uma receita avulsa Efetivada por pessoa, com o total recebido
dela, fora do cartão, na conta e na data informadas e com mês contábil da data. A descrição é o
nome da pessoa e a subcategoria é `Reembolsos > Reembolsos recebidos`, criadas na primeira vez.
Descrição desativada bloqueia a efetivação. Total R$ 0,00 encerra os itens sem lançamento, e a
conta só é obrigatória se alguma pessoa tiver total maior que zero.

**Atomicidade e repetição.** Tudo roda em uma única `IFinancialOperation`: qualquer falha desfaz
também as receitas das outras pessoas. Um item já recebido, removido ou com esperado diferente do
mostrado rejeita a efetivação inteira.

**Proteções.** Enquanto estiver Recebido, a despesa não pode ter valor, tipo, pessoa ou percentual
alterados nem ser excluída; os demais campos continuam editáveis, e o item acompanha o mês contábil
do lançamento. A receita gerada não pode ser editada nem excluída em Lançamentos. A pessoa com
recebimento não pode ser excluída.

**Desfazer ([#83](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/83)).** Um
recebimento pode ser desfeito, ao contrário de um pagamento de fatura. O alcance é o recebimento
inteiro da pessoa, porque a receita cobre todos os itens dela naquela efetivação; os de outras pessoas
não são afetados. A tela mostra o que será desfeito e pede confirmação. Em uma única operação, a receita
é excluída, com seus anexos indo para a lixeira, os itens voltam a Pendente e as proteções da despesa
deixam de valer. Não há histórico de recebimentos desfeitos.

O modelo está em [Reembolsos](../architecture/reimbursements.md).
