# Bancos, contas e cartões

Origem: [issue #6](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/6).

**Estado:** cadastro e desativação estão implementados. Arquivar/desarquivar (#53),
[Logos de banco (#54)](bank-logos.md) estão implementados; carteira visual (#55) permanece pendente.
A persistência usa [PostgreSQL](../architecture/persistence-postgresql.md) (#60): tabelas no schema `accounts`.

Em **Cadastros > Contas e cartões**, cadastrar bancos pelo nome; cadastrar e editar contas
Corrente, Investimento (incluindo caixinhas e CDB, sem rendimento) ou Poupança.
Cada conta exige um banco existente, nome de até 100 caracteres e cor da paleta.
O nome é único por banco após remover espaços nas extremidades e ignorar maiúsculas;
contas desativadas continuam reservando seu nome. Valores iniciais, transferências e
ajustes serão tratados nas issues #46, #47 e #48.

Cartões exigem conta corrente ativa, nome, bandeira Visa/Mastercard/Elo, limite não
negativo, cor, fechamento e vencimento entre 1 e 31. A opção de incluir o dia do
fechamento é explícita; conferir os valores com o emissor ao configurar o cartão.
A paleta oferece verde, violeta, laranja, rosa e cinza, usando os tokens do tema.

## Decisões de design

- O domínio valida os dados; a aplicação valida referências e duplicidade.
  Falhas esperadas retornam mensagem, sem exceção para o componente.
- `IAccountCatalog` expõe DTOs com ExternalId, nunca entidades ou Id interno.
  Consultas trazem apenas ativos por padrão; consultas históricas aceitam inativos.
- A ação Excluir/desativar sempre oferece confirmação de desativação. Não há exclusão
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
