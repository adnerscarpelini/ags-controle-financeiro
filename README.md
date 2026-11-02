# ags-controle-financeiro

Aplicativo desktop de controle financeiro pessoal, para Windows e Linux, feito em .NET 10 com Avalonia UI e SQLite.

Substitui uma planilha de receitas e despesas e mantém a mesma lógica: **mês contábil** (o mês em que o valor pesa no orçamento), cadastros fechados, contas fixas, faturas de cartão com pagamentos antecipados e reembolsos rateados.

> Projeto pessoal e de estudo. O repositório é público, mas não contém dados financeiros reais: testes e exemplos usam somente dados fictícios.

## Arquitetura

Monólito modular com Clean Architecture em cada módulo:

| Módulo | Responsável por |
| --- | --- |
| `Accounts` | Contas, cartões e calendário de fatura |
| `Transactions` | Categorias, estabelecimentos, lançamentos, parcelas e contas fixas |
| `Invoices` | Faturas por cartão, pagamentos antecipados e cashback |
| `Reimbursements` | Pessoas, regras de rateio e reembolsos |
| `Reports` | Dashboard e análises |
| `Imports` | Importação da planilha e, depois, de faturas |

Detalhes em [`docs/architecture`](docs/architecture/module-structure.md).

## Como executar

> A solução ainda está sendo criada. Os comandos abaixo passam a valer a partir da primeira entrega.

```bash
dotnet build Ags.Finance.slnx
dotnet test Ags.Finance.slnx
dotnet run --project src/Ags.Finance.Desktop
```

Requer o SDK do .NET 10.

## Como contribuir

Requisitos ficam nas [issues](../../issues). Cada issue tem sua branch e entra por pull request na `main`. Padrões completos em [`CLAUDE.md`](CLAUDE.md).
