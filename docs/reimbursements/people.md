# Pessoas de reembolso

A [#78](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/78) cadastra
as pessoas que devolvem parte das despesas do usuário, em `/cadastros/pessoas`
(`IPersonCatalog`, em `Contracts`).

**Pessoa.** Nome único (sem distinguir caixa, acentos ou pontuação) e percentual padrão
de reembolso, obrigatório, de 0 a 100, com até duas casas decimais.

**Percentual padrão.** Só pré-preenche novas seleções. Alterá-lo não muda lançamentos
nem séries já gravados.

**Persistência.** Schema `reimbursements`, tabela `people`. O percentual é `numeric(5,2)`.

**Exclusão.** Uma pessoa usada em lançamentos ou séries não pode ser excluída. O
arquivamento de pessoas não existe.

**CSV.** O tipo `people` está na [versão 2 do layout](../imports/csv-layout.md).

O modelo e as decisões estão em [Reembolsos](../architecture/reimbursements.md).
