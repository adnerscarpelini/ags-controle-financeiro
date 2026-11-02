<p align="center">
  <img src="logo.png" alt="Logo do ags-controle-financeiro: carteira verde com moeda e gráfico em alta" width="160">
</p>

<h1 align="center">ags-controle-financeiro</h1>

Aplicação web pessoal em .NET 10, ASP.NET Core e Blazor, acessada pelo computador e celular na rede local.

O destino aprovado é **Docker Compose + PostgreSQL**, no Linux ou Windows via WSL 2.
A persistência já usa PostgreSQL ([#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60));
o Compose da aplicação ([#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59)) vem a seguir.

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

Até a #59, a aplicação roda com `dotnet run` contra um PostgreSQL que você sobe à parte. Os testes
relacionais também exigem um PostgreSQL real
([detalhes](docs/architecture/persistence-postgresql.md#testes-relacionais)).

```bash
docker compose -f tests/postgres/compose.yaml up -d --wait
export AGS_TEST_POSTGRES="Host=127.0.0.1;Port=54329;Username=postgres;Password=ags_test_only"
dotnet build Ags.Finance.slnx
dotnet test Ags.Finance.slnx
dotnet format Ags.Finance.slnx --verify-no-changes
export ConnectionStrings__Finance="Host=127.0.0.1;Port=54329;Database=ags_finance_dev;Username=postgres;Password=ags_test_only"
dotnet run --project src/Ags.Finance.Web
```

Esse PostgreSQL de testes é descartável e vale só para desenvolvimento (crie o banco `ags_finance_dev` antes do
`dotnet run`). Requer o SDK .NET 10 e Docker Engine (no Windows, dentro do WSL 2). A aplicação atende em `https://localhost:5280`; a porta é configurável.
HTTP redireciona para HTTPS e o certificado local precisa ser confiado no dispositivo.
Crie o usuário por [comando](docs/identity/user-creation.md). A
[hospedagem](docs/architecture/web-hosting.md) distingue execução atual e destino aprovado;
os scripts nativos são legado a substituir na #59.

## Próxima entrega

A [#59, fixada como próxima prioridade](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59),
é concluída após #60: volumes persistentes, backup sob demanda e teste real
de recuperação em instalação vazia. Não haverá migração SQLite nem suporte duplo.

Decisões: [persistência PostgreSQL](docs/architecture/persistence-postgresql.md) e
[consistência financeira](docs/architecture/financial-consistency.md).
Requisitos e dúvidas ainda abertas permanecem nas issues.

## Como contribuir

[Issues](https://github.com/adnerscarpelini/ags-controle-financeiro/issues) são a fonte dos requisitos.
O trabalho é organizado em lotes: um commit por issue e um PR por lote para a main.
Issues avulsas usam branch própria. O usuário revisa e faz o merge.
Padrões completos em [CLAUDE.md](CLAUDE.md).
