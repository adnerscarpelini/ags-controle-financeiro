# Documentação

- [Protótipo da nova interface (#95–#103)](architecture/ui-prototype.md)
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
- [Pessoas de reembolso (#78)](reimbursements/people.md)
- [Pessoa e percentual de reembolso em despesas e recorrentes (#79/#80)](transactions/entry-reimbursement.md)
- [Reembolsos do mês (#81)](reimbursements/monthly-reimbursements.md)
- [Recebimento de reembolsos e como desfazê-lo (#82/#83)](reimbursements/receiving.md)
- [Resumo do mês (#24)](reports/monthly-summary.md)
- [Despesas e receitas por categoria (#25)](reports/category-report.md)
- [Faturas no dashboard (#112)](reports/invoices-dashboard.md)
- [Planejamento mensal com metas (#94)](reports/monthly-planning.md)
- [Relatórios: visão de 19 meses (#77/#94)](reports/planning.md)
- [Pendências do dashboard: hoje até 7, 15 ou 30 dias (#58/#69)](reports/pending-agenda.md)
- [Preferências do aplicativo: tema, tela inicial, dashboard, paginação e sessão (#69)](preferences/settings.md)
- [Recorrentes previstas na fatura (#69)](invoices/recurring-forecast.md)
- [Layout CSV oficial, exportação e importação de cadastros e de lançamentos (#61, #62, #28, #29)](imports/csv-layout.md)

Esta pasta registra comportamento implementado e decisões de arquitetura.
Requisitos, critérios de aceite e dúvidas permanecem nas issues.
**Decisão aprovada não significa implementação concluída:** cada nota indica seu estado.

## Arquitetura

- [Estrutura e estado dos módulos](architecture/module-structure.md)
- [Testes de arquitetura](architecture/architecture-tests.md)
- [Persistência PostgreSQL — implementada (#60)](architecture/persistence-postgresql.md)
- [SharedKernel](architecture/shared-kernel.md)
- [Design da interface](architecture/ui-design.md)
- [Biblioteca de componentes: MudBlazor (#95)](architecture/ui-library.md)
- [Hospedagem e distribuição Docker — implementada (#59)](architecture/web-hosting.md)
- [Consistência financeira — decisões e fronteiras de domínio](architecture/financial-consistency.md)
- [Reembolsos — modelo e fronteiras](architecture/reimbursements.md)

A persistência usa PostgreSQL (#60) e a distribuição é Docker Compose (#59), com backup e recuperação (#31).
Não há migração de dados SQLite. O restante do produto continua nas issues,
incluindo a validação em campo da PWA (#38).

## Funcionalidades implementadas

- SharedKernel: [Money e ReferenceMonth](shared-kernel/money-and-reference-month.md)
- Web: [Layout e navegação](web/layout-and-navigation.md)
- Web: [Instalação com Docker Compose e backup (#59/#31)](web/docker-deployment.md)
- Web: [Certificado HTTPS](web/https-certificate.md)
- Web: [Instalação como PWA (#38)](web/pwa.md)
- Web: [Ocultar valores e cor da barra do navegador (#71)](web/hide-values.md)
- Identity: [Criação e redefinição do usuário](identity/user-creation.md)
- Identity: [Login com usuário e senha](identity/login.md)
- Accounts: [Bancos, contas e cartões](accounts/banks-accounts-and-cards.md)
- Accounts: [Finais de cartão](accounts/card-last-digits.md)
- Accounts: [Mês de referência de uma compra](accounts/purchase-reference-month.md)

O Lote 07 entrega o núcleo financeiro e os cadastros visuais descritos neste índice.
A previsão consolidada de caixa da #58 continua pendente; efetivação e consulta de pendências (#56/#57) estão implementadas.

- [Saldos e parcelas futuras (#20)](invoices/future-obligations.md)

- [Antecipação de parcelas (#32)](invoices/installment-anticipation.md)

- [Ajuste de total de fatura (#52)](invoices/total-adjustments.md)

- [Arquivamento reversível de contas/cartões (#53)](accounts/archiving.md)

- [Logos de bancos (#54)](accounts/bank-logos.md)
- [Armazenamento e processamento de logos (#54)](architecture/bank-logo-storage.md)

- [Carteira visual de cartões (#55)](accounts/card-wallet.md)

- [Registro de erros inesperados (#107)](diagnostics/error-records.md)
- [Armazenamento de anexos](architecture/attachments-storage.md)
- [Documentos anexados](attachments/document-storage.md)

- [Documentos de lançamentos](attachments/entry-documents.md)

- [Documentos de faturas e pagamentos](attachments/invoice-documents.md)

- [Backup, reconciliação e exportação de anexos](attachments/backup-and-export.md)

- [Repetir lançamentos fora do cartão](transactions/repeated-entries.md) (#91).
