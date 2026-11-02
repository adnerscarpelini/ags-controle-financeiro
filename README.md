<p align="center">
  <img src="logo.png" alt="Logo do ags-controle-financeiro: carteira verde com moeda e gráfico em alta" width="160">
</p>

<h1 align="center">ags-controle-financeiro</h1>

Aplicação web de controle financeiro pessoal, feita em .NET 10 com ASP.NET Core, Blazor e SQLite. Roda como serviço no Windows ou no Linux, como o Syncthing, e é acessada pelo navegador do computador e do celular na rede local.

Substitui uma planilha de receitas e despesas e mantém a mesma lógica: **mês contábil** (o mês em que o valor pesa no orçamento), cadastros fechados, contas fixas, faturas de cartão com pagamentos antecipados e reembolsos rateados.

> Projeto pessoal e de estudo. O repositório é público, mas não contém dados financeiros reais: testes e exemplos usam somente dados fictícios.

## Arquitetura

Monólito modular com Clean Architecture em cada módulo:

| Módulo | Responsável por |
| --- | --- |
| `Identity` | Usuário, login e senha |
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
dotnet run --project src/Ags.Finance.Web
```

Requer o SDK do .NET 10. A instalação como serviço (Windows e Linux) e a criação do usuário ficam documentadas em [`docs/architecture/web-hosting.md`](docs/architecture/web-hosting.md).

## Como contribuir

Requisitos ficam nas [issues](../../issues). Cada issue tem sua branch e entra por pull request na `main`. Padrões completos em [`CLAUDE.md`](CLAUDE.md).
