# Persistência com PostgreSQL

## Estado e contexto

**Implementado na [#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60).**
O código usa Npgsql/PostgreSQL e não há mais SQLite. A distribuição com Docker Compose é a
[#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59), e backup, agenda, retenção e restauração
são a [#31](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/31); a operação está em
[docker-deployment.md](../web/docker-deployment.md).

A aplicação roda num único computador da rede local, em uso por uma pessoa. A regra herdada do
monólito modular é que cada módulo seja dono dos seus dados. O projeto não tinha uso real quando a
troca foi feita: os dados e as migrations de desenvolvimento foram descartados e recriados, sem
conversão do SQLite e sem manter dois providers.

## Decisão

- PostgreSQL é o único banco, em serviço separado da aplicação no Compose.
- Um banco, com **um schema por módulo persistente** (`identity`, `accounts`, `transactions`,
  `invoices`, `reimbursements`, `preferences`, `attachments`), **um `DbContext` e um histórico de migrations próprios** por módulo
  (`<schema>.__ef_migrations`). Reports e Imports não têm tabelas.
- Money continua decimal no domínio e centavos em `BIGINT`; AccountingMonth continua inteiro `yyyyMM`
  (`INTEGER`). `ExternalId` é Guid v7 gerado no domínio (`UUID`, índice único); `Id` é `BIGINT` identity;
  `CreatedAt`/`UpdatedAt` são `timestamptz` em UTC, preenchidos por um interceptor com o `TimeProvider`.
- Sem FK, JOIN, SQL ou acesso a tabelas que cruze módulos. Schemas separam a organização, mas **não
  substituem a fronteira dos Contracts**: uma referência a outro módulo guarda o `ExternalId`. A
  [tabela de dependências](module-structure.md) continua sem ciclos.
- PostgreSQL usa volume local próprio. `DataDirectory` fica para certificados, Data Protection e
  arquivos da aplicação. Logos normalizados ficam no próprio banco (#54). Dados ativos não ficam em pasta sincronizada.
- Pacotes: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 (provider do EF Core 10, nos módulos) e `Npgsql`
  10.0.3 (verificação de prontidão no host e nos testes). As versões ficam em `Directory.Packages.props`.
- Versão do servidor: PostgreSQL 18 (os testes e o CI fixam `postgres:18.6-alpine`). Atualizar o major
  do banco exige procedimento próprio.

## Implementação

### Conexão e inicialização

- A conexão vem de `ConnectionStrings:Finance` (variável `ConnectionStrings__Finance`). Sem ela, ou sem
  `Host`, `Database` e `Username`, a aplicação e os comandos de usuário terminam com erro claro. Mensagens
  e logs nunca repetem a connection string, que contém a senha, e ela não é versionada. No desenvolvimento,
  use variável de ambiente ou `dotnet user-secrets`.
- `DatabaseStartup` (`Ags.Finance.Web/Persistence`) espera o PostgreSQL aceitar conexões
  (`Database:ReadyTimeoutSeconds`, padrão 60 s), tentando de novo a cada 2 s. Credenciais recusadas ou banco
  inexistente falham na hora, pois esperar não resolve. Depois aplica as migrations pendentes de cada módulo,
  em sequência. Em qualquer falha a aplicação registra o erro e sai com código 1 **antes de começar a
  atender**, sem servir com o banco parcial.
- O host passa a connection string a cada módulo: `Add<Module>Module(this IServiceCollection, string)`. O
  módulo não conhece o host nem lê configuração.
- `DataDirectoryResolver` escolhe a pasta de arquivos da aplicação (certificados e chaves; logos ficam no PostgreSQL).

### Padrão em cada módulo

| Arquivo | Responsabilidade |
| --- | --- |
| `Domain/Entity.cs` | Base com `Id` (`long`), `ExternalId` (`Guid.CreateVersion7()`), `CreatedAt` e `UpdatedAt`. Só BCL; os setters são privados porque quem preenche é a Infrastructure. |
| `Infrastructure/<Module>DbContext.cs` | Constantes `Schema` e `MigrationsHistoryTable`; define o schema padrão e aplica os `IEntityTypeConfiguration<T>` do assembly. |
| `Infrastructure/PersistenceConventions.cs` | Ignora a base `Entity`; define a chave `Id` e o índice único de `ExternalId`; registra os conversores. |
| `Infrastructure/ValueConverters.cs` | `Money` ↔ centavos (`BIGINT`); `AccountingMonth` ↔ inteiro `yyyyMM` (janeiro de 2027 = `202701`). |
| `Infrastructure/AuditTimestampsInterceptor.cs` | `SaveChangesInterceptor` que preenche `CreatedAt`/`UpdatedAt` (UTC) com o `TimeProvider`; nunca regrava `CreatedAt` em atualização. |
| `Infrastructure/Migrations/` | Migration `Initial` (cria o schema e as tabelas) e o snapshot do módulo. |
| `DependencyInjection.cs` | `Add<Module>Module(connectionString)` registra `TimeProvider.System` (se ausente), o interceptor e o `DbContext` com `UseNpgsql` e o histórico no schema do módulo; `Migrate<Module>ModuleAsync` aplica as migrations. |

Como usar numa entidade nova:

1. Herde de `Entity` (construtor protegido sem argumentos ou construtor cujos parâmetros casam com propriedades mapeadas).
2. Crie um `IEntityTypeConfiguration<T>` na `Infrastructure` com `builder.ToTable("entries")`; o schema do módulo vem do `HasDefaultSchema`. Propriedades `Money` e `AccountingMonth` não precisam de configuração.
3. Gere a migration do módulo com o histórico no schema dele e atualize o snapshot.
4. Tipos `get`-only não são mapeados pelo EF; use `private set`.

Colunas mantêm os nomes PascalCase das propriedades (entre aspas em SQL manual). Uma violação de
unicidade esperada, como o nome duplicado por banco, é detectada pelo `SqlState` `23505` do
`PostgresException` e volta pelo fluxo de negócio (`AccountsStore.SaveAsync`).

### Testes relacionais

Testes que tocam o banco usam um **PostgreSQL real**, nunca SQLite ou provider em memória.
`Ags.Finance.Tests.Support.PostgresTestDatabase` cria um banco vazio e exclusivo por teste (`ags_test_<guid>`) no
servidor indicado pela variável `AGS_TEST_POSTGRES` e o apaga ao final. Sem a variável, o teste falha com
instrução. Localmente, no Linux ou no WSL 2:

```bash
docker compose -f tests/postgres/compose.yaml up -d --wait
export AGS_TEST_POSTGRES="Host=127.0.0.1;Port=54329;Username=postgres;Password=ags_test_only"
dotnet test Ags.Finance.slnx
docker compose -f tests/postgres/compose.yaml down
```

No Windows, com Docker Engine dentro do WSL 2, a porta publicada em `127.0.0.1` do WSL fica acessível pelo
Windows. O CI roda a solução completa num runner Linux com PostgreSQL; o runner Windows compila e roda só
os testes de domínio e de arquitetura. A senha do `compose.yaml` de testes é descartável e não vale para
instalações reais.

Cobertura: `MigrationTests` (cada módulo) provam o schema, o histórico próprio, a reaplicação sem duplicar,
que todas as tabelas ficam no schema e que snapshot e modelo coincidem; `PersistenceConventionsTests`
(Transactions) prova centavos em `bigint`, `yyyyMM`, `ExternalId` v7 com índice único, `Id` identity e
carimbos UTC de um relógio falso; `DatabaseStartupTests` (Web) sobe a aplicação, confere os cinco schemas, a
ausência de FK entre módulos e o tratamento de configuração ausente, credenciais erradas e banco
indisponível.

### Limpeza do desenvolvimento anterior

`scripts/clean-legacy-dev-data.ps1` apaga somente `finance.db`, `finance.db-wal` e `finance.db-shm` da
pasta de dados padrão do `dotnet run` (aceita `-WhatIf` e `-DataDirectory`). Não toca em certificados nem em
nenhuma outra pasta.

## Recuperação e atualização

Implementados na [#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59) (backup sob demanda e
ensaio de recuperação) e na [#31](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/31) (interface, agenda,
retenção e restauração em manutenção): `Ags.Finance.Web/Backup`, descritos em [docker-deployment.md](../web/docker-deployment.md).

Usar pg_dump/pg_restore com versões compatíveis. O procedimento precisa preparar papéis e
permissões, preservar arquivos associados e testar uma instalação vazia, conferindo login,
identidades, vínculos e totais. Uma cópia concluída precisa existir fora da máquina/disco original.

Volumes persistentes protegem a recriação de containers, mas não garantem recuperação da perda
do host. CSV é troca de dados do sistema e não substitui esse backup. A recuperação lógica
retorna ao estado do backup concluído; recuperação por instante/WAL não está aprovada neste escopo.

O reset autorizado valeu para a transição de desenvolvimento. Atualizações posteriores
preservam os dados e exigem backup antes de migrations; retornar à imagem anterior não desfaz
as mudanças do banco.

## Consequências e alternativas

- As consultas que cruzam módulos (por exemplo, o dashboard) compõem dados em memória a partir dos
  `Contracts`. Com dezenas de milhares de lançamentos isso é aceitável.
- Uma operação atômica entre dois módulos não é garantida; a escolha do banco não autoriza acesso cruzado
  nem sucesso parcial silencioso. Se a necessidade aparecer, a fronteira deve ser revista antes.
- PostgreSQL adiciona serviço, credenciais e preparação de recuperação, com instalação padronizada pelo
  Compose e um processo de restauração verificável.
- **Um `DbContext` compartilhado:** mais simples, mas apaga as fronteiras entre módulos.
- **Um banco por módulo:** isolamento total, mas complica backup, restauração e a operação do serviço.
- **Manter SQLite em paralelo:** duplicaria migrations e testes, sem necessidade aprovada.
- **Nomes em snake_case:** exigiria um pacote a mais para o provider; adiado.

Anexos (#73) guardam só metadados no schema `attachments`; os originais ficam em pasta própria. Ver [decisão](attachments-storage.md). Não estão no arquivo `app-files.tar.gz`.

O backup v2 (#76) acrescenta `attachments.json` por pacote e uma cópia única incremental em `<backup>/attachments/`, conferida por tamanho e SHA-256. A lixeira também entra. Os índices dos pacotes mantidos determinam a retenção desses arquivos. Formato v1 continua aceito; migrations recriam o schema `attachments` após restaurar um dump anterior ao módulo. A pasta de backup inteira precisa ser copiada para fora da máquina.
