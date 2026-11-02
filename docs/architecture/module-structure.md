# Estrutura de módulos

## Decisão

O aplicativo é um **monólito modular**. Um único executável (`Ags.Finance.Desktop`, Avalonia) compõe módulos independentes, e cada módulo é uma class library organizada em Clean Architecture.

**Estado:** aprovado. Os projetos ainda não foram criados.

## Módulos

| Módulo | Projeto | Responsável por | Pode consumir `Contracts` de |
| --- | --- | --- | --- |
| Accounts | `Ags.Finance.Modules.Accounts` | Contas, cartões, finais de cartão, calendário de fatura (fechamento e vencimento) | — |
| Transactions | `Ags.Finance.Modules.Transactions` | Categorias, estabelecimentos, lançamentos, compras parceladas, contas fixas | Accounts |
| Invoices | `Ags.Finance.Modules.Invoices` | Faturas por cartão e mês, pagamentos antecipados, cashback e descontos | Accounts, Transactions |
| Reimbursements | `Ags.Finance.Modules.Reimbursements` | Pessoas, regras de rateio, valores a receber e recebidos | Accounts, Transactions |
| Reports | `Ags.Finance.Modules.Reports` | Dashboard e análises, somente leitura | todos |
| Imports | `Ags.Finance.Modules.Imports` | Importação da planilha; depois, OFX e PDF de faturas | todos |

As dependências apontam sempre na direção da tabela, sem ciclos. Uma dependência nova precisa ser registrada aqui.

## Camadas dentro de cada módulo

| Pasta | Responsabilidade | Pode depender de |
| --- | --- | --- |
| `Domain` | Entidades, objetos de valor, invariantes e regras de negócio | BCL e `SharedKernel` |
| `Application` | Casos de uso, uma pasta por funcionalidade | `Domain`, `Contracts` próprios e de módulos permitidos |
| `Infrastructure` | `DbContext`, mapeamentos, migrations, serviços externos | `Application` e `Domain` |
| `Contracts` | API pública do módulo | BCL e `SharedKernel` |
| `DependencyInjection.cs` | Registro do módulo (`Add<Module>Module`) | todas as pastas do módulo |

Somente `Contracts` e `DependencyInjection` são públicos. Os testes de arquitetura verificam essas regras.

## Host

`Ags.Finance.Desktop` é o composition root: registra os módulos, contém Views e ViewModels (MVVM) e o serviço de backup. ViewModels chamam casos de uso e nunca acessam a `Infrastructure` de um módulo.

## Alternativas consideradas

- **Projeto único com pastas por camada:** mais simples, mas sem fronteiras verificáveis entre domínios.
- **Microsserviços:** sem sentido para um app desktop de um usuário.
- **Menos módulos** (Catálogo dentro de Transactions, Reembolsos dentro de Invoices): considerado; mantidos separados porque têm regras e motivos de mudança distintos.
