# Estrutura de módulos

## Decisão

A aplicação é um **monólito modular**. Um único host web (`Ags.Finance.Web`, ASP.NET Core + Blazor) compõe módulos independentes, e cada módulo é uma class library organizada em Clean Architecture.

**Estado:** aprovado. Os projetos ainda não foram criados.

## Módulos

| Módulo | Projeto | Responsável por | Pode consumir `Contracts` de |
| --- | --- | --- | --- |
| Identity | `Ags.Finance.Modules.Identity` | Usuário único, hash de senha, comandos `user create` e `user reset-password` | — |
| Accounts | `Ags.Finance.Modules.Accounts` | Bancos, contas, cartões (bandeira, limite), finais de cartão, calendário de fatura | — |
| Transactions | `Ags.Finance.Modules.Transactions` | Categorias, descrições, lançamentos, compras parceladas, lançamentos recorrentes, filtros salvos | Accounts |
| Invoices | `Ags.Finance.Modules.Invoices` | Faturas por cartão e mês, pagamentos antecipados, cashback, descontos, antecipação de parcelas | Accounts, Transactions |
| Reimbursements | `Ags.Finance.Modules.Reimbursements` | Pessoas, regras de rateio (filtro salvo + percentual), valores a receber e recebidos | Accounts, Transactions |
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

`Ags.Finance.Web` é o composition root: registra os módulos, configura autenticação, HTTPS e a execução como serviço, contém as páginas e componentes Blazor e o serviço de backup. Componentes chamam casos de uso pelos `Contracts` e nunca acessam a `Infrastructure` de um módulo. Detalhes de hospedagem em [web-hosting.md](web-hosting.md).

## Alternativas consideradas

- **Projeto único com pastas por camada:** mais simples, mas sem fronteiras verificáveis entre domínios.
- **Microsserviços:** sem sentido para uma aplicação de um usuário.
- **Host desktop (Avalonia):** foi a primeira escolha; trocado por web para permitir o acesso pelo celular na rede local.
- **Menos módulos** (Catálogo dentro de Transactions, Reembolsos dentro de Invoices): considerado; mantidos separados porque têm regras e motivos de mudança distintos.
