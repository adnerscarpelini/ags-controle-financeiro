# Pessoas e regras de rateio

A [#21](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/21)
cadastra as pessoas que reembolsam o usuário e as regras que dizem quanto cada uma
deve: um [filtro salvo](../transactions/saved-filters.md) (#33), um percentual e um
mês inicial.

**Pessoa.** Nome único (sem distinguir caixa, acentos ou pontuação) e a descrição do
pagador usada no lançamento de receita dos reembolsos recebidos (#23); sem ela, vale
o nome. Uma pessoa com regras não pode ser excluída.

**Regra.** Percentual de 0 a 100 com até duas casas; fora disso é rejeitado. Não pode
haver duas regras com a mesma pessoa, o mesmo filtro e o mesmo mês inicial. A regra
guarda apenas o `ExternalId` do filtro: ela sempre usa os critérios **atuais** do
filtro, em qualquer mês, inclusive passado. Nada é congelado nem há histórico de
valores devidos (fora do escopo; o cálculo é a #22).

**Exclusão de filtro.** Reimbursements implementa `ISavedFilterUsageGuard`; excluir um
filtro usado por uma regra é bloqueado, indicando as pessoas envolvidas.

**Persistência.** Schema `reimbursements`: `people` e `sharing_rules` (chave para a
pessoa dentro do módulo; o filtro, de outro módulo, é referenciado só pelo
`ExternalId`, sem FK entre schemas). O módulo passou a usar `IDbContextFactory` com o
`NpgsqlDataSource` compartilhado, como os demais.

A tela `/reembolsos` cadastra, edita e exclui pessoas e regras.
