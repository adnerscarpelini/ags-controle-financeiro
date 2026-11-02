# Documentação

Esta pasta registra comportamento implementado e decisões de arquitetura.
Requisitos, critérios de aceite e dúvidas permanecem nas issues.
**Decisão aprovada não significa implementação concluída:** cada nota indica seu estado.

## Arquitetura

- [Estrutura e estado dos módulos](architecture/module-structure.md)
- [Testes de arquitetura](architecture/architecture-tests.md)
- [Persistência PostgreSQL — aprovada, pendente #60](architecture/persistence-postgresql.md)
- [Persistência SQLite — código atual/legado em transição](architecture/persistence-sqlite.md)
- [SharedKernel](architecture/shared-kernel.md)
- [Design da interface](architecture/ui-design.md)
- [Hospedagem e distribuição Docker — aprovada, pendente #59](architecture/web-hosting.md)
- [Consistência financeira — decisões e fronteiras de domínio](architecture/financial-consistency.md)

A próxima entrega é #60 → #59. Não há migração de dados SQLite; os dados de desenvolvimento
podem ser recriados dentro do escopo autorizado. O restante do produto continua nas issues,
incluindo CSV oficial (#28/#29/#61/#62) e recuperação PostgreSQL (#31).

## Funcionalidades implementadas

- SharedKernel: [Money e AccountingMonth](shared-kernel/money-and-accounting-month.md)
- Web: [Layout e navegação](web/layout-and-navigation.md)
- Web: [Instalação nativa atual — substituição prevista #59](web/service-installation.md)
- Web: [Certificado HTTPS](web/https-certificate.md)
- Identity: [Criação e redefinição do usuário](identity/user-creation.md)
- Identity: [Login com usuário e senha](identity/login.md)
- Accounts: [Bancos, contas e cartões](accounts/banks-accounts-and-cards.md)
- Accounts: [Finais de cartão](accounts/card-last-digits.md)
- Accounts: [Mês contábil de uma compra](accounts/purchase-accounting-month.md)

Arquivamento reversível (#53), logos (#54), carteira visual (#55), faturas/pagamentos/ajustes
(#18/#19/#50–#52) e efetivação/pendentes (#56–#58) ainda não são apresentados como recursos entregues.
