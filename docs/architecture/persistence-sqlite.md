# Persistência com SQLite

## Contexto

A aplicação roda como serviço num único computador da rede local e precisa de um banco em arquivo, fácil de copiar para backup. A regra herdada do monólito modular é que cada módulo seja dono dos seus dados.

## Decisão

- Um único arquivo SQLite (`finance.db`) na pasta de dados configurável `DataDirectory`. Padrões: `%ProgramData%\AgsFinance` no Windows e `/var/lib/ags-finance` no Linux quando roda como serviço; a pasta local do usuário em desenvolvimento. Nunca numa pasta sincronizada (Google Drive, OneDrive): a sincronização durante uma gravação pode corromper o arquivo.
- Como o SQLite não tem schemas, cada módulo tem seu `DbContext`, um **prefixo de tabela** (`identity_`, `accounts_`, `transactions_`, `invoices_`, `reimbursements_`) e sua **própria tabela de histórico de migrations** (`__ef_migrations_<module>`).
- Sem FK, JOIN ou SQL entre tabelas de módulos diferentes. Uma referência a outro módulo guarda o `ExternalId`.
- Valores monetários em centavos (`INTEGER`), convertidos para `Money` no mapeamento.
- Modo WAL ativado.
- Backup pela API de backup do SQLite (`SqliteConnection.BackupDatabase`) para uma pasta configurável, que pode ser a pasta do Google Drive para computador.

## Consequências

- As consultas que cruzam módulos (por exemplo, o dashboard) compõem dados em memória a partir dos `Contracts`. Com dezenas de milhares de lançamentos isso é aceitável.
- Uma operação atômica entre dois módulos não é garantida. Se aparecer essa necessidade, a fronteira deve ser revista antes da implementação.

## Alternativas consideradas

- **Um `DbContext` compartilhado:** mais simples, mas apaga as fronteiras entre módulos.
- **Um arquivo `.db` por módulo:** isolamento total, mas complica backup e restauração.
