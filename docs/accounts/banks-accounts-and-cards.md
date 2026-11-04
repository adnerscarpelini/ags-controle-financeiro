# Bancos, contas e cartões

Origem: [issue #6](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/6).

**Estado:** cadastros, [arquivamento reversível (#53)](archiving.md),
[logos de banco (#54)](bank-logos.md) e [carteira visual (#55)](card-wallet.md) estão implementados.
A persistência usa [PostgreSQL](../architecture/persistence-postgresql.md) (#60): tabelas no schema `accounts`.

Em **Cadastros > Bancos**, **Contas** e **Cartões**, cadastrar bancos pelo nome; cadastrar e editar contas
Corrente, Investimento (incluindo caixinhas e CDB, sem rendimento) ou Poupança.
Cada conta exige um banco existente, nome de até 100 caracteres e cor da paleta.
O nome é único por banco após remover espaços nas extremidades e ignorar maiúsculas;
contas arquivadas continuam reservando seu nome. [Saldo inicial](../architecture/account-balances.md),
[transferências](../transactions/account-transfers.md) e [ajustes de saldo](../transactions/balance-adjustments.md) estão implementados (#46/#47/#48).

## Bancos

O nome do banco é único, sem considerar espaços nas extremidades nem maiúsculas, e bancos arquivados
continuam reservando o nome. O nome pode ser editado sem afetar contas e logo, que apontam para o `ExternalId`.

- **Sem conta vinculada, inclusive arquivada:** o banco pode ser excluído, junto do logo. A exclusão é física.
- **Com conta vinculada:** a exclusão é recusada e a ação oferecida é arquivar.
- **Arquivado:** sai da lista padrão e da seleção de novas contas e pode ser desarquivado. Contas já vinculadas
  continuam mostrando o banco e podem ser editadas; uma conta nova ou que mude de banco não pode usá-lo.

A atualização acrescenta o `Id` ao nome de bancos repetidos já gravados, para o índice único não impedir a
migration. O layout CSV de bancos não traz a situação: um banco arquivado é exportado como os demais e, importado em
uma instalação vazia, entra ativo.

## Contas e cartões

Cartões exigem conta corrente ativa, nome, bandeira Visa/Mastercard/Elo, limite não
negativo, cor, fechamento e vencimento entre 1 e 31. A opção de incluir o dia do
fechamento é explícita; conferir os valores com o emissor ao configurar o cartão.
A paleta oferece verde, violeta, laranja, rosa e cinza, usando os tokens do tema.

## Decisões de design

- O domínio valida os dados; a aplicação valida referências e duplicidade.
  Falhas esperadas retornam mensagem, sem exceção para o componente.
- `IAccountCatalog` expõe DTOs com ExternalId, nunca entidades ou Id interno.
  Consultas trazem apenas ativos por padrão; consultas históricas aceitam inativos.
- Em contas e cartões, a ação Excluir/desativar sempre oferece confirmação de desativação. Não há exclusão
  física, mesmo sem uso conhecido: preserva referências sem criar dependência inversa
  de Accounts para Transactions. Cancelar não altera o cadastro.
- Desativar conta retira seus cartões das seleções, preservando os registros.
  Uma conta vinculada a cartão não pode mudar para Investimento/Poupança.
- A persistência usa contextos curtos por operação via DbContextFactory, evitando
  manter um contexto rastreado durante todo o circuito Blazor. O índice único no
  PostgreSQL também protege a duplicidade de nome por banco em gravações concorrentes.
- Tabelas accounts.banks, accounts.accounts e accounts.credit_cards seguem as
  convenções do módulo (schema próprio); limite em centavos, ExternalId v7 e auditoria UTC.
  Não foram adicionados pacotes nem dependências entre módulos.

## Validação

Testes de integração com PostgreSQL real cobrem cadastros, validações, limites, duplicidade,
edição, preservação de inativos, migrations e armazenamento monetário.
Testes de componentes percorrem os formulários e a confirmação de desativação.
