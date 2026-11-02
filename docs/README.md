# Documentação

- [Edição, exclusão e movimentação de lançamentos (#14)](transactions/entry-changes.md)

- [Compras parceladas (#13)](transactions/installments.md)

- [Situação e fechamento de faturas (#50)](invoices/lifecycle.md)

- [Atomicidade e proteção entre módulos (#50)](architecture/financial-operations.md)

- [Consulta e composição de faturas (#18)](invoices/composition.md)

- [Compras no cartão e escolha de fatura (#12)](transactions/card-entries.md)

- [Receitas e despesas avulsas (#11)](transactions/cash-entries.md)

- [Saldo inicial e responsabilidade pelo saldo (#47)](architecture/account-balances.md)

- [Categorias e subcategorias (#9)](transactions/classifications.md)
- [Descrições sem duplicidade (#10)](transactions/merchants.md)

Esta pasta registra comportamento implementado e decisões de arquitetura.
Requisitos, critérios de aceite e dúvidas permanecem nas issues.
**Decisão aprovada não significa implementação concluída:** cada nota indica seu estado.

## Arquitetura

- [Estrutura e estado dos módulos](architecture/module-structure.md)
- [Testes de arquitetura](architecture/architecture-tests.md)
- [Persistência PostgreSQL — implementada (#60)](architecture/persistence-postgresql.md)
- [SharedKernel](architecture/shared-kernel.md)
- [Design da interface](architecture/ui-design.md)
- [Hospedagem e distribuição Docker — implementada (#59)](architecture/web-hosting.md)
- [Consistência financeira — decisões e fronteiras de domínio](architecture/financial-consistency.md)

A persistência usa PostgreSQL (#60) e a distribuição é Docker Compose (#59), com backup e recuperação (#31).
Não há migração de dados SQLite. O restante do produto continua nas issues,
incluindo o CSV oficial (#28/#29/#61/#62).

## Funcionalidades implementadas

- SharedKernel: [Money e AccountingMonth](shared-kernel/money-and-accounting-month.md)
- Web: [Layout e navegação](web/layout-and-navigation.md)
- Web: [Instalação com Docker Compose e backup (#59/#31)](web/docker-deployment.md)
- Web: [Certificado HTTPS](web/https-certificate.md)
- Identity: [Criação e redefinição do usuário](identity/user-creation.md)
- Identity: [Login com usuário e senha](identity/login.md)
- Accounts: [Bancos, contas e cartões](accounts/banks-accounts-and-cards.md)
- Accounts: [Finais de cartão](accounts/card-last-digits.md)
- Accounts: [Mês contábil de uma compra](accounts/purchase-accounting-month.md)

Arquivamento reversível (#53), logos (#54), carteira visual (#55), faturas/pagamentos/ajustes
(#18/#19/#50–#52) e efetivação/pendentes (#56–#58) ainda não são apresentados como recursos entregues.
