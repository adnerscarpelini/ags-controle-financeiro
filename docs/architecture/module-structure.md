# Estrutura de módulos

## Decisão

A aplicação é um **monólito modular**. Um único host web (`Ags.Finance.Web`, ASP.NET Core + Blazor) compõe módulos independentes, e cada módulo é uma class library organizada em Clean Architecture.

**Estado:** esqueleto e testes de arquitetura entregues (#1/#2). Identity tem usuário, login e senha; Accounts tem bancos/logos, contas com abertura, cartões/finais, calendário e arquivamento (#6–#8/#47/#53–#55). Transactions tem classificações, descrições e lançamentos, parcelas, séries com alcance, baixa, pendências, transferências e ajustes de saldo/fatura (#9–#17/#46/#48/#52/#56/#57); Invoices tem consulta, ciclo, pagamentos, abatimentos, quitação, saldos futuros e antecipação (#18–#20/#32/#50/#51). Transactions também tem filtros salvos (#33) e totais por mês (`IEntryTotals`); Reimbursements tem pessoas com percentual padrão, análise do mês, recebimento e desfazer (#78/#81–#83), com pessoa e percentual na despesa (#79/#80/#84); Reports tem resumo, categorias, faturas, mapa de calor, pendências e planejamento (#24–#27/#58/#77); Imports exporta e importa cadastros e lançamentos em CSV (#28/#29/#61/#62); Preferences guarda as parametrizações do aplicativo (#69). As demais operações continuam nas issues. PostgreSQL por schema (#60), distribuição Docker (#59) e backup (#31) estão implementados. A tabela abaixo inclui responsabilidades futuras.

## Módulos

| Módulo | Projeto | Responsável por | Pode consumir `Contracts` de |
| --- | --- | --- | --- |
| Attachments | `Ags.Finance.Modules.Attachments` | Arquivos originais, metadados, lixeira e integridade (#73) | — |
| Identity | `Ags.Finance.Modules.Identity` | Usuário único, hash de senha, comandos `user create` e `user reset-password` | — |
| Accounts | `Ags.Finance.Modules.Accounts` | Bancos/logos, contas, cartões/finais, calendário e arquivamento cadastral | — |
| Transactions | `Ags.Finance.Modules.Transactions` | Categorias, descrições, lançamentos, parcelas, recorrência, efetivação e filtros salvos | Accounts, Attachments |
| Invoices | `Ags.Finance.Modules.Invoices` | Faturas e suas situações, pagamentos, cashback, descontos, ajustes e antecipação de parcelas | Accounts, Transactions, Preferences, Attachments |
| Preferences | `Ags.Finance.Modules.Preferences` | Parametrizações do aplicativo: tema, tela inicial, recorrentes na fatura, dashboard, paginação e duração da sessão (#69) | — |
| Reimbursements | `Ags.Finance.Modules.Reimbursements` | Pessoas e percentual padrão, reembolsos a receber e recebidos ([modelo](reimbursements.md)) | Accounts, Transactions |
| Reports | `Ags.Finance.Modules.Reports` | Dashboard e análises, somente leitura | Identity, Accounts, Transactions, Invoices, Reimbursements, Preferences |
| Imports | `Ags.Finance.Modules.Imports` | Importação/exportação CSV no layout oficial e versionado do sistema (#28/#29/#61/#62) | Identity, Accounts, Transactions, Invoices, Reimbursements |

As dependências apontam sempre na direção da tabela, sem ciclos. `Reports` e `Imports` consomem os módulos de negócio (`Identity`, `Accounts`, `Transactions`, `Invoices`, `Reimbursements`), mas não um ao outro, para evitar ciclo. `Preferences` não depende de nenhum módulo; quem precisa de uma preferência (Invoices e Reports) a lê pelos `Contracts` dela, e Transactions continua sem consumi-la: o chamador informa o que for preciso. Uma dependência nova precisa ser registrada aqui.

## Camadas dentro de cada módulo

| Pasta | Responsabilidade | Pode depender de |
| --- | --- | --- |
| `Domain` | Entidades, objetos de valor, invariantes e regras de negócio | BCL e `SharedKernel` |
| `Application` | Casos de uso, uma pasta por funcionalidade | `Domain`, `Contracts` próprios e de módulos permitidos |
| `Infrastructure` | `DbContext`, mapeamentos, migrations, serviços externos | `Application` e `Domain` |
| `Contracts` | API pública do módulo | BCL, `SharedKernel` e DTOs dos `Contracts` de módulos permitidos para composição |
| `DependencyInjection.cs` | Registro do módulo (`Add<Module>Module`) | todas as pastas do módulo |

Somente `Contracts` e `DependencyInjection` são públicos. Os testes de arquitetura verificam essas regras.

## Host

`Ags.Finance.Web` é o composition root: registra os módulos, configura autenticação, HTTPS e execução, contém páginas e componentes Blazor e será responsável pela operação de backup. A distribuição é Docker Compose (#59), com backup e recuperação (#31). Componentes usam os `Contracts`, nunca a `Infrastructure` de um módulo. Ver [hospedagem](web-hosting.md), [persistência aprovada](persistence-postgresql.md) e [consistência financeira](financial-consistency.md).

## Alternativas consideradas

- **Projeto único com pastas por camada:** mais simples, mas sem fronteiras verificáveis entre domínios.
- **Microsserviços:** sem sentido para uma aplicação de um usuário.
- **Host desktop (Avalonia):** foi a primeira escolha; trocado por web para permitir o acesso pelo celular na rede local.
- **Menos módulos** (Catálogo dentro de Transactions, Reembolsos dentro de Invoices): considerado; mantidos separados porque têm regras e motivos de mudança distintos.

Attachments não depende de outros módulos. Transactions e Invoices validam o dono antes de chamar seus Contracts. Não há FK ou JOIN entre módulos. A data do upload define o caminho imutável, conforme [armazenamento](attachments-storage.md).

Na composição de documentos (#74–#76), os Contracts de Transactions e Invoices reutilizam os DTOs pequenos de Attachments. Isso segue as dependências permitidas, como a composição de lançamentos nos Contracts de Invoices, sem expor entidades ou Infrastructure. As referências para o ZIP são consultas dos dados já persistidos: não geram recorrências nem fecham faturas.
