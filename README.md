<p align="center">
  <img src="logo.png" alt="Logo do ags-controle-financeiro: carteira verde com moeda e gráfico em alta" width="160">
</p>

<h1 align="center">ags-controle-financeiro</h1>

Aplicação web pessoal em .NET 10, ASP.NET Core e Blazor, acessada pelo computador e celular na rede local.

O destino aprovado é **Docker Compose + PostgreSQL**, no Linux ou Windows via WSL 2.
A migração ainda será implementada na ordem [#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60) →
[#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59).
O código atual ainda executa nativamente com SQLite.

O conceito central é o **mês contábil**, quando o valor pesa no orçamento. Cadastros de bancos,
contas, cartões/finais, calendário de compras, HTTPS e login estão implementados.
Lançamentos, recorrências com baixa manual, faturas, pagamentos, ajustes e reembolsos
têm entregas abertas nas issues.

Importação/exportação serão somente por **CSV no layout oficial do sistema**.
Não há escopo de PDF, OFX, XLSX, layouts bancários ou migração da antiga planilha.
Backup completo e recuperação usam o fluxo PostgreSQL aprovado, separado do CSV.

> Projeto pessoal e de estudo. O repositório é público; testes e exemplos usam somente dados fictícios.

## Arquitetura

Monólito modular com Clean Architecture em cada módulo. As responsabilidades aprovadas são:

| Módulo | Responsável por |
| --- | --- |
| Identity | Usuário, login e senha |
| Accounts | Bancos, contas, cartões/finais, arquivamento e calendário de fatura |
| Transactions | Categorias, descrições, lançamentos, parcelas, recorrência e efetivação |
| Invoices | Situações de fatura, pagamentos, cashback e ajustes |
| Reimbursements | Pessoas, regras de rateio e reembolsos |
| Reports | Dashboard e análises |
| Imports | Importação/exportação CSV no layout oficial do sistema |

[Estado e fronteiras dos módulos](docs/architecture/module-structure.md).

## Executar o código atual

Até #60/#59, este caminho usa SQLite e execução nativa. Ainda não há Compose/PostgreSQL
entregue neste estado do repositório.

```bash
dotnet build Ags.Finance.slnx
dotnet test Ags.Finance.slnx
dotnet format Ags.Finance.slnx --verify-no-changes
dotnet run --project src/Ags.Finance.Web
```

Requer o SDK .NET 10. A aplicação atende em `https://localhost:5280`; a porta é configurável.
HTTP redireciona para HTTPS e o certificado local precisa ser confiado no dispositivo.
Crie o usuário por [comando](docs/identity/user-creation.md). A
[hospedagem](docs/architecture/web-hosting.md) distingue execução atual e destino aprovado;
os scripts nativos são legado a substituir na #59.

## Próxima entrega

A [#59, fixada como próxima prioridade](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59),
é concluída após #60: PostgreSQL único, volumes persistentes, backup sob demanda e teste real
de recuperação em instalação vazia. Os dados de desenvolvimento deste projeto podem ser
descartados nessa transição; não haverá migração SQLite nem suporte duplo.

Decisões: [persistência PostgreSQL](docs/architecture/persistence-postgresql.md) e
[consistência financeira](docs/architecture/financial-consistency.md).
Requisitos e dúvidas ainda abertas permanecem nas issues.

## Como contribuir

[Issues](https://github.com/adnerscarpelini/ags-controle-financeiro/issues) são a fonte dos requisitos.
O trabalho é organizado em lotes: um commit por issue e um PR por lote para a main.
Issues avulsas usam branch própria. O usuário revisa e faz o merge.
Padrões completos em [CLAUDE.md](CLAUDE.md).
