# Persistência com SQLite

## Contexto

A aplicação roda como serviço num único computador da rede local e precisa de um banco em arquivo, fácil de copiar para backup. A regra herdada do monólito modular é que cada módulo seja dono dos seus dados.

## Decisão

- Um único arquivo SQLite (`finance.db`) na pasta de dados configurável `DataDirectory`. Padrões: `%ProgramData%\AgsFinance` no Windows e `/var/lib/ags-finance` no Linux quando roda como serviço; a pasta local do usuário em desenvolvimento. Nunca numa pasta sincronizada (Google Drive, OneDrive): a sincronização durante uma gravação pode corromper o arquivo.
- Como o SQLite não tem schemas, cada módulo tem seu `DbContext`, um **prefixo de tabela** (`identity_`, `accounts_`, `transactions_`, `invoices_`, `reimbursements_`) e sua **própria tabela de histórico de migrations** (`__ef_migrations_<module>`).
- Sem FK, JOIN ou SQL entre tabelas de módulos diferentes. Uma referência a outro módulo guarda o `ExternalId`.
- Valores monetários em centavos (`INTEGER`), convertidos para `Money` no mapeamento.
- Modo WAL ativado na inicialização.
- Backup pela API de backup do SQLite (`SqliteConnection.BackupDatabase`) para uma pasta configurável, que pode ser a pasta do Google Drive para computador.

## Implementação (issue #4)

**Estado:** implementado nos módulos com persistência (Identity, Accounts, Transactions, Invoices e Reimbursements). Reports e Imports não têm tabelas. Só o Identity tem entidade (`identity_users`, migration `CreateUsers`); os demais têm só a migration `Initial`, vazia, que cria o histórico. As tabelas chegam com as issues de cada módulo.

### Pasta de dados e inicialização

- `DataDirectoryResolver` (`Ags.Finance.Web/Persistence`) escolhe a pasta: o valor de `DataDirectory` em `appsettings.json` (ou variável de ambiente) quando informado; senão, rodando como serviço, `%ProgramData%\AgsFinance` (Windows) ou `/var/lib/ags-finance` (Linux); senão (`dotnet run`), a pasta local do usuário (`%LocalAppData%\AgsFinance` ou `~/.local/share/ags-finance`). "Rodando como serviço" é detectado por `Environment.UserInteractive` no Windows e pela variável `INVOCATION_ID` do systemd no Linux; a issue do serviço pode trocar isso pelos helpers do `Microsoft.Extensions.Hosting`.
- `DatabaseStartup` cria a pasta, monta a connection string de `finance.db` e, antes de a aplicação atender requisições, ativa o modo WAL (`PRAGMA journal_mode=WAL`, que fica gravado no arquivo) e chama `Migrate<Module>ModuleAsync` de cada módulo, na ordem de dependência. Só migrations pendentes são aplicadas.
- O host passa a connection string a cada módulo: `Add<Module>Module(this IServiceCollection services, string connectionString)`. O módulo não conhece o host nem lê configuração.

### Padrão do módulo (copiar ao criar a primeira entidade de um módulo)

O padrão vive em cada módulo, sem projeto compartilhado, para não extrair código cedo demais. Os arquivos de Transactions são a referência; os demais módulos têm a mesma cópia com o nome trocado.

| Arquivo | Responsabilidade |
| --- | --- |
| `Domain/Entity.cs` | Base com `Id` (`long`), `ExternalId` (`Guid.CreateVersion7()`), `CreatedAt` e `UpdatedAt`. Só BCL; os setters são privados porque quem preenche é a Infrastructure. |
| `Infrastructure/<Module>DbContext.cs` | Constantes `TablePrefix` (`transactions_`) e `MigrationsHistoryTable` (`__ef_migrations_transactions`); aplica os `IEntityTypeConfiguration<T>` do assembly. |
| `Infrastructure/PersistenceConventions.cs` | Ignora a base `Entity`; depois dos mapeamentos, acrescenta o prefixo aos nomes de tabela, define a chave `Id` e o índice único de `ExternalId`; registra os conversores. |
| `Infrastructure/ValueConverters.cs` | `Money` ↔ centavos (`INTEGER`); `AccountingMonth` ↔ inteiro `yyyyMM` (janeiro de 2027 = `202701`). |
| `Infrastructure/AuditTimestampsInterceptor.cs` | `SaveChangesInterceptor` que preenche `CreatedAt`/`UpdatedAt` (UTC) com o `TimeProvider`; nunca regrava `CreatedAt` em atualização. |
| `Infrastructure/Migrations/` | Migrations e snapshot do módulo. |
| `DependencyInjection.cs` | `Add<Module>Module(connectionString)` registra `TimeProvider.System` (se ausente), o interceptor e o `DbContext` com `UseSqlite` e a tabela de histórico própria; `Migrate<Module>ModuleAsync` aplica as migrations. |

Como usar numa entidade nova:

1. Herde de `Entity` (construtor protegido sem argumentos ou construtor cujos parâmetros casam com propriedades mapeadas).
2. Crie um `IEntityTypeConfiguration<T>` na `Infrastructure` com `builder.ToTable("entries")`, **sem** o prefixo; ele é acrescentado pela convenção. Propriedades `Money` e `AccountingMonth` não precisam de configuração.
3. Adicione o `DbSet` ao `DbContext` (ou deixe o mapeamento descobrir a entidade) e gere a migration do módulo, apontando a migrations para a tabela de histórico do módulo.
4. Tipos `get`-only não são mapeados pelo EF; use `private set`.

### Detalhes e armadilhas

- O EF Core cria no SQLite a tabela `__EFMigrationsLock`, interna e compartilhada por todos os módulos, para serializar migrations. Ela é a única tabela sem prefixo além dos históricos `__ef_migrations_<module>`.
- Enquanto um módulo não tem `IEntityTypeConfiguration`, o EF avisa que não encontrou mapeamentos; o registro do módulo ignora esse aviso (`CoreEventId.NoEntityTypeConfigurationsWarning`). O log do EF fica em `Warning` no `appsettings.json`, para não registrar cada comando SQL.
- As migrations dos módulos rodam em sequência no startup, não em paralelo.
- `DateTimeOffset` é gravado como texto pelo provedor SQLite; como tudo é UTC, a leitura devolve o mesmo valor.
- Testes: `MigrationTests` (em cada módulo) provam o arquivo criado, o histórico próprio e o prefixo; `PersistenceConventionsTests` (Transactions) prova centavos, `yyyyMM`, `ExternalId` v7 com índice único e carimbos de um relógio falso; `DatabaseStartupTests` (Web) sobe a aplicação com uma pasta temporária e confere criação, migrations de todos os módulos, reinício sem duplicar e WAL.

## Consequências

- As consultas que cruzam módulos (por exemplo, o dashboard) compõem dados em memória a partir dos `Contracts`. Com dezenas de milhares de lançamentos isso é aceitável.
- Uma operação atômica entre dois módulos não é garantida. Se aparecer essa necessidade, a fronteira deve ser revista antes da implementação.

## Alternativas consideradas

- **Um `DbContext` compartilhado:** mais simples, mas apaga as fronteiras entre módulos.
- **Um arquivo `.db` por módulo:** isolamento total, mas complica backup e restauração.
