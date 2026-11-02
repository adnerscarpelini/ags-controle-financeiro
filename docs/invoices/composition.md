# Consulta de faturas

A [#18](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/18)
entrega Faturas por cartão e mês contábil, com total e composição. Invoices
consome os Contracts de Transactions e Accounts, sem acessar seus schemas.

A consulta agrupa o mês gravado em cada compra, preservando escolhas manuais.
Receitas/despesas à vista não compõem faturas. O total é a soma exata dos itens;
cartões sem composição ou registro não geram linhas artificiais. Cadastros
inativos continuam identificando o histórico. Consultar não modifica os dados.

Situação e fechamento estão implementados na [#50](lifecycle.md). Faturas registradas
manualmente também aparecem, mesmo sem compras. Abatimentos/pagamentos (#19/#51),
parcelas futuras (#20) e ajustes (#52) serão integrados nas próximas etapas.
