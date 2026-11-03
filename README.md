<p align="center">
  <img src="logo.png" alt="Logo do ags-controle-financeiro: carteira verde com moeda e gráfico em alta" width="160">
</p>

<h1 align="center">ags-controle-financeiro</h1>

Aplicação web pessoal em .NET 10, ASP.NET Core e Blazor, acessada pelo computador e celular na rede local.

O destino aprovado é **Docker Compose + PostgreSQL**, no Linux ou Windows via WSL 2.
A aplicação roda com **Docker Compose + PostgreSQL** ([#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60),
[#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59)), com backup e recuperação
([#31](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/31)).

O conceito central é o **mês contábil**, quando o valor pesa no orçamento. Cadastros de bancos,
contas com saldo inicial, cartões/finais, categorias, descrições, calendário de compras,
HTTPS e login estão implementados. Há lançamentos à vista e no cartão e consulta,
fechamento e reabertura de faturas, compras parceladas e séries mensais previstas com edição por alcance, baixa manual, visão de pendências, transferências e ajustes de saldo de contas.
Há cadastro e histórico anual de cashback/desconto, com abatimento das faturas.
Pagamentos têm débito atômico e quitação explícita, com consulta de saldos e parcelas futuras. Ajustes de fatura preservam compras e pagamentos; antecipação move as últimas parcelas futuras para a fatura atual.
Contas/cartões têm arquivamento reversível, logos locais com recorte circular e carteira visual responsiva.
Reembolsos têm pessoas, regras de rateio por filtro salvo, apuração mensal e registro de recebimento. O dashboard mostra resumo do mês, categorias contra a média, faturas por cartão, mapa de calor e pendências dos próximos sete dias.
A aplicação pode ser instalada como PWA no celular (validação em aparelho e na rede local ainda pendente).

Importação/exportação são somente por **CSV no layout oficial do sistema** (cadastros e lançamentos).
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

## Instalar e executar

Com Docker Engine e Compose (Linux, ou Windows com Docker Engine no WSL 2; Docker Desktop não é necessário):

```bash
cp .env.example .env && chmod 600 .env      # preencha as senhas e AGS_HOST_NAMES
sudo install -d -o 1654 -g 1654 -m 0750 backups
docker compose up -d --wait
docker compose run --rm app user create
```

Acesse `https://<nome-ou-ip>:5280` e confie no certificado. Instalação completa, rede no WSL, inicialização automática,
atualização e backup/recuperação: [docs/web/docker-deployment.md](docs/web/docker-deployment.md).

### Desenvolvimento

Requer o SDK .NET 10 e um PostgreSQL. Os testes relacionais usam PostgreSQL real
([detalhes](docs/architecture/persistence-postgresql.md#testes-relacionais)):

```bash
docker compose -f tests/postgres/compose.yaml up -d --wait
export AGS_TEST_POSTGRES="Host=127.0.0.1;Port=54329;Username=postgres;Password=ags_test_only"
dotnet build Ags.Finance.slnx
dotnet test Ags.Finance.slnx
dotnet format Ags.Finance.slnx --verify-no-changes
```

Para `dotnet run`, crie um banco (`CREATE DATABASE ags_finance_dev`) nesse mesmo servidor de testes e exporte
`ConnectionStrings__Finance="Host=127.0.0.1;Port=54329;Database=ags_finance_dev;Username=postgres;Password=ags_test_only"`
(descartável; só para desenvolvimento) antes de `dotnet run --project src/Ags.Finance.Web`. Em desenvolvimento a aplicação
atende em `https://localhost:5280`.

Decisões: [persistência PostgreSQL](docs/architecture/persistence-postgresql.md),
[hospedagem](docs/architecture/web-hosting.md) e [consistência financeira](docs/architecture/financial-consistency.md).
Requisitos e dúvidas ainda abertas permanecem nas issues.

## Como contribuir

[Issues](https://github.com/adnerscarpelini/ags-controle-financeiro/issues) são a fonte dos requisitos.
O trabalho é organizado em lotes: um commit por issue e um PR por lote para a main.
Issues avulsas usam branch própria. O usuário revisa e faz o merge.
Padrões completos em [CLAUDE.md](CLAUDE.md).
