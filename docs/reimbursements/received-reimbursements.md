# Reembolso recebido

A [#23](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/23)
registra o dinheiro que uma pessoa devolveu (`IReimbursementReceipts.RegisterAsync`:
pessoa, data, valor e conta ativa). O valor precisa ser maior que zero.

**Um único efeito financeiro.** O recebimento cria **uma** receita avulsa Efetivada,
fora do cartão, em Transactions (via `IEntryCatalog`), com data e conta informadas,
mês contábil do mês da data, descrição igual ao pagador da pessoa e subcategoria
`Reembolsos > Reembolsos recebidos`. A conta é creditada uma única vez e não nasce
pendência de baixa. Categoria, subcategoria e descrição do pagador são criadas na
primeira vez e reutilizadas depois; uma descrição do pagador desativada bloqueia o
registro com uma mensagem, em vez de criar duplicata.

**Atomicidade entre módulos.** A receita (schema `transactions`) e o vínculo
`receipts` (schema `reimbursements`) são gravados dentro de `IFinancialOperation`,
que abre uma transação ambiente sobre a mesma conexão e serializa operações
financeiras. Cada módulo continua escrevendo só no próprio schema, pelos Contracts
do outro. Se o vínculo falhar depois de a receita ser criada, tudo é desfeito
(coberto por teste).

**Saldo.** O recebido do mês abate o devido da [apuração](monthly-statement.md). Mais
do que o devido deixa saldo negativo, exibido como crédito da pessoa. Uma pessoa com
recebimentos não pode ser excluída. Recebimento parcial distribuído entre meses está
fora do escopo.
