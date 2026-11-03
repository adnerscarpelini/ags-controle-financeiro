# Documentação

- [Consulta de pendências e baixa (#57)](transactions/pending-entries.md)
- [Ajustes de saldo (#48)](transactions/balance-adjustments.md)
- [Transferências e movimentos de caixa (#46)](transactions/account-transfers.md)
- [Séries, ocorrências, alcance e baixa manual (#16/#17/#56)](transactions/recurrences.md)

- [Filtros e totais dos lançamentos (#15)](transactions/entry-filters.md)

- [Edição, exclusão e movimentação de lançamentos (#14)](transactions/entry-changes.md)

- [Compras parceladas (#13)](transactions/installments.md)

- [Situação e fechamento de faturas (#50)](invoices/lifecycle.md)

- [Atomicidade e proteção entre módulos (#50)](architecture/financial-operations.md)

- [Pagamentos, abatimentos, débito e quitação (#19/#51)](invoices/payments.md)
- [Consulta e composição de faturas (#18)](invoices/composition.md)

- [Compras no cartão e escolha de fatura (#12)](transactions/card-entries.md)

- [Receitas e despesas avulsas (#11)](transactions/cash-entries.md)

- [Saldo inicial e responsabilidade pelo saldo (#47)](architecture/account-balances.md)

- [Categorias e subcategorias (#9)](transactions/classifications.md)
- [Descrições sem duplicidade (#10)](transactions/merchants.md)
- [Filtros salvos de lançamentos (#33)](transactions/saved-filters.md)
- [Pessoas e regras de rateio (#21)](reimbursements/people-and-sharing-rules.md)
- [Valor a receber de uma pessoa no mês (#22)](reimbursements/monthly-statement.md)
- [Reembolso recebido (#23)](reimbursements/received-reimbursements.md)
- [Resumo do mês (#24)](reports/monthly-summary.md)
- [Despesas e receitas por categoria (#25)](reports/category-report.md)
- [Faturas por cartão no dashboard (#26)](reports/invoices-dashboard.md)
- [Mapa de calor de despesas (#27)](reports/heat-map.md)

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

O Lote 07 entrega o núcleo financeiro e os cadastros visuais descritos neste índice.
A previsão consolidada de caixa da #58 continua pendente; efetivação e consulta de pendências (#56/#57) estão implementadas.

- [Saldos e parcelas futuras (#20)](invoices/future-obligations.md)

- [Antecipação de parcelas (#32)](invoices/installment-anticipation.md)

- [Ajuste de total de fatura (#52)](invoices/total-adjustments.md)

- [Arquivamento reversível de contas/cartões (#53)](accounts/archiving.md)

- [Logos de bancos (#54)](accounts/bank-logos.md)
- [Armazenamento e processamento de logos (#54)](architecture/bank-logo-storage.md)

- [Carteira visual de cartões (#55)](accounts/card-wallet.md)
