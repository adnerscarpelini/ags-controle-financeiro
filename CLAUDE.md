# CLAUDE.md

Instruções para agentes de IA que trabalham neste repositório. Leia este arquivo inteiro antes de planejar, implementar, revisar ou documentar qualquer coisa.

## O projeto

`ags-controle-financeiro` é uma aplicação web pessoal de controle financeiro que substitui uma planilha do Google Sheets. Ela roda como **serviço** no Windows ou no Linux (como o Syncthing) e é acessada pelo navegador do computador e do celular na **rede local**. Tem um único usuário e não depende de internet.

- **Stack:** .NET 10, C#, ASP.NET Core com Blazor Web App (render mode Interactive Server), EF Core com SQLite, xUnit. Roda como Windows Service ou serviço systemd, servindo HTTPS na rede local.
- **Repositório público:** nunca versione dados financeiros reais, nomes de estabelecimentos reais do usuário, extratos, faturas, planilhas ou o arquivo do banco. Testes e exemplos usam somente dados fictícios.
- **Idioma:** converse, escreva issues, PRs, commits e documentação em **português**. Escreva **todo o código em inglês** (tipos, propriedades, métodos, variáveis, tabelas, colunas, testes), seguindo as convenções oficiais da Microsoft para C#/.NET. Textos exibidos na interface ficam em português.

## Arquitetura

Monólito modular com Clean Architecture dentro de cada módulo. A fonte de verdade é `docs/architecture/`; em caso de divergência, siga a documentação e aponte a diferença.

```
src/
  Ags.Finance.Web/                # host ASP.NET Core + Blazor: composition root, páginas, componentes, serviço, backup
  Ags.Finance.SharedKernel/       # somente Money e AccountingMonth
  Modules/
    Ags.Finance.Modules.<Module>/
      Domain/                     # entidades, objetos de valor, invariantes; só depende da BCL e do SharedKernel
      Application/<UseCase>/      # casos de uso, uma pasta por funcionalidade
      Infrastructure/             # DbContext, mapeamentos, migrations, serviços externos
      Contracts/                  # API pública do módulo para outros módulos
      DependencyInjection.cs      # Add<Module>Module(this IServiceCollection)
tests/
  Ags.Finance.Modules.<Module>.Tests/
  Ags.Finance.ArchitectureTests/
```

Módulos: `Identity`, `Accounts`, `Transactions`, `Invoices`, `Reimbursements`, `Reports` e `Imports`. Responsabilidades e dependências permitidas estão em `docs/architecture/module-structure.md`.

### Regras de fronteira

- `internal` e `sealed` por padrão. Só são `public` a classe `DependencyInjection` e os tipos em `Contracts`. Testes acessam tipos internos via `InternalsVisibleTo`.
- Um módulo só referencia outro pelos `Contracts`, numa direção única e sem ciclos. Uma nova dependência entre módulos precisa ser registrada em `docs/architecture/`.
- `Contracts` expõe DTOs e interfaces pequenas, com o `ExternalId` (`Guid`). Nunca exponha entidades ou o `Id` interno.
- `Domain` não depende de EF Core, ASP.NET Core, Blazor nem de outras pastas. `Application` depende de `Domain` e de `Contracts`.
- Páginas e componentes Blazor chamam casos de uso pelos `Contracts` dos módulos, nunca a `Infrastructure`. Regra de negócio não fica em componente.
- O `SharedKernel` contém apenas `Money` e `AccountingMonth`. Não adicione nada a ele sem uma decisão registrada.
- Não adote CQRS, MediatR, Repository genérico, Unit of Work extra ou mensageria sem um problema concreto discutido antes.

### Persistência (SQLite)

- Um único arquivo `.db`, mas um `DbContext` por módulo, com prefixo de tabela do módulo (`transactions_entries`) e tabela de histórico de migrations própria (`__ef_migrations_<module>`).
- Sem FK, JOIN ou SQL que cruze tabelas de módulos diferentes. Uma referência a outro módulo guarda o `ExternalId` dele.
- Toda entidade com tabela própria tem `Id` (`long`, chave primária), `ExternalId` (`Guid`, gerado no domínio com `Guid.CreateVersion7()`, índice único), `CreatedAt` e `UpdatedAt` (`DateTimeOffset` UTC, vindos de `TimeProvider`, preenchidos por um interceptor do módulo).
- Mapeamentos com `IEntityTypeConfiguration<T>` na `Infrastructure`; o domínio não tem atributos de persistência.
- Valores monetários: `decimal` no código (`Money`), centavos em `INTEGER` no banco. Nunca `float` ou `double`.
- O arquivo do banco fica na pasta de dados do serviço (`DataDirectory`; padrão `%ProgramData%\AgsFinance` no Windows e `/var/lib/ags-finance` no Linux), nunca numa pasta sincronizada. Backup é feito pela API de backup do SQLite.

### Regras de negócio centrais

Leia as issues antes de implementar. As regras mais sensíveis são:

- **AccountingMonth:** no débito, PIX ou dinheiro, é o mês da data. No cartão, é o mês do vencimento da fatura em que a compra cai, conforme o dia de fechamento do cartão. O valor calculado é uma sugestão editável e é gravado.
- **Parcelas:** uma compra em N vezes gera N lançamentos em meses contábeis consecutivos. A diferença de centavos vai para a primeira parcela.
- **Pagamento de fatura não é despesa:** é um `InvoicePayment`. Despesas já estão lançadas item a item.

Não invente regras de negócio. Se uma issue não define um comportamento, pergunte.

## Convenções de código

- Siga `.editorconfig`: 4 espaços, chaves em linhas separadas (Allman), `using System` primeiro, LF.
- Namespaces de arquivo, seguindo a pasta: `Ags.Finance.Modules.Transactions.Application.CreateEntry`.
- Nomes: PascalCase para tipos e membros, camelCase para parâmetros e variáveis, `_camelCase` para campos privados, `I` para interfaces, sufixo `Async` para métodos aguardáveis.
- Nullable habilitado; não use `!` sem garantia. `TreatWarningsAsErrors` está ativo: corrija avisos em vez de suprimi-los.
- Versões de pacotes ficam em `Directory.Packages.props`; nos `.csproj`, apenas `<PackageReference Include="..." />`. Justifique toda dependência nova.
- Interface só quando houver fronteira (persistência, relógio, sistema de arquivos) ou mais de uma implementação real. Evite `Manager`, `Helper`, `Utils`.
- Falhas esperadas (regra violada, item inexistente) voltam no fluxo de retorno, não como exceção.
- async/await com `CancellationToken` em I/O. Nada de `.Result` ou `.Wait()`.

## Design da interface

Tema **escuro por padrão**, visual moderno e limpo, com um verde "dinheiro" como cor principal. Os tokens ficam como variáveis CSS num único arquivo; não use cores soltas nos componentes. A interface é responsiva: funciona no computador e no celular, e pode ser instalada como PWA. Detalhes em `docs/architecture/ui-design.md`.

| Token | Cor | Uso |
| --- | --- | --- |
| `Background` | `#0E1311` | fundo da janela |
| `Surface` | `#151C19` | cards, painéis, grade |
| `SurfaceRaised` | `#1C2521` | linha selecionada, menus, hover |
| `Border` | `#27332E` | bordas e divisórias |
| `TextPrimary` | `#E6EDE9` | texto principal |
| `TextSecondary` | `#93A39B` | rótulos e texto secundário |
| `Accent` | `#10B981` | cor principal: botões, item ativo, foco, destaques |
| `AccentHover` | `#34D399` | hover e estados ativos do accent |
| `Income` | `#34D399` | receitas e valores positivos |
| `Expense` | `#F87171` | despesas e valores negativos |
| `Warning` | `#FBBF24` | alertas (teto estourado, fatura em aberto) |

- CSS próprio com os tokens; biblioteca de componentes só se justificada numa issue. O tema claro pode existir depois, mas o escuro é o padrão.
- Nada carregado de CDN: fontes, scripts e ícones são servidos pela própria aplicação.
- Fonte Inter (servida localmente), números com dígitos tabulares e alinhados à direita. Valores em `R$ 1.234,56`, datas em `dd/MM/yyyy` (cultura `pt-BR`).
- Cantos arredondados (8), espaçamento em múltiplos de 4, sem sombras pesadas nem gradientes.
- O verde é a cor de marca; receitas usam o verde claro e despesas o vermelho. Não use cor como único sinal: valores negativos também levam o sinal.
- Cores de banco só no texto, em peso normal: Itaú `#FB923C`, Nubank `#A78BFA`, Bradesco `#F87171`.

## Segurança

- Toda página e endpoint exige login, exceto a página de login e os arquivos estáticos.
- O único usuário é criado pelo comando `user create` no computador do serviço (chamado pelo script de instalação). A aplicação web nunca oferece cadastro de usuário; a senha é trocada logado ou pelo comando `user reset-password`.
- Senhas só em hash (`PasswordHasher` do ASP.NET Core Identity). Bloqueio temporário após tentativas erradas.
- HTTPS com certificado local gerado na primeira execução; HTTP redireciona para HTTPS.
- Não registre senhas, cookies ou dados financeiros em logs.

## Testes e validação

- Teste regras de domínio e casos de uso pelo comportamento observável: caminho feliz, falhas e limites (fim de mês, dia de fechamento, centavos de parcelas).
- Testes de integração de persistência usam SQLite real em arquivo temporário ou em memória (`DataSource=:memory:` com conexão aberta).
- Mantenha os testes de arquitetura (`Ags.Finance.ArchitectureTests`) cobrindo as regras de fronteira. Uma nova regra de fronteira ganha um teste.
- Antes de entregar, execute: `dotnet build Ags.Finance.slnx` (zero avisos), `dotnet test Ags.Finance.slnx` e `dotnet format Ags.Finance.slnx --verify-no-changes`.
- Nunca declare um teste aprovado sem tê-lo executado. Relate o que foi executado e as limitações.

## Fluxo de trabalho

### Issues

- Os requisitos vivem em GitHub Issues. Não crie specs ou arquivos de requisitos no repositório; `docs/` descreve só o comportamento implementado e as decisões de arquitetura.
- Título: `[<Module>] <ação no infinitivo>` (ex.: `[Transactions] Lançar compra parcelada`). Tarefas sem módulo usam `[Transversal]`.
- Corpo segue os templates de `.github/ISSUE_TEMPLATE/`. Critérios de aceite no formato *Dado / Quando / Então*, incluindo falhas e limites.
- Labels: uma de tipo (`feature`, `bug`, `refactor`, `architecture`, `maintenance`) e uma `module:<nome>` por módulo afetado.
- Issues pequenas (INVEST): uma funcionalidade por issue, que caiba num PR revisável. Requisito novo descoberto durante o trabalho vira outra issue, não escopo extra.
- Funcionalidade ou regra nova sem issue: primeiro analise, discuta e proponha o rascunho; só registre após aprovação explícita do usuário.

### Branches e pull requests

- GitHub Flow. `main` é protegida: sem push direto, sem force push, histórico linear, merge só por squash via PR.
- Branch por issue a partir da `main` atualizada: `<tipo>/<N>-<descricao-curta>` (`feature`, `fix`, `refactor`, `docs`, `chore`). Ex.: `feature/12-create-entry`.
- Título do PR em Conventional Commits, em português, com a issue: `feat: lança compra parcelada (#12)`.
- Descrição do PR com as seções do template: Resumo, Decisões de design, QA, Documentação e `Closes #N`.

### Divisão de responsabilidades

1. O agente implementa, executa as validações e apresenta o diff, as evidências e as limitações.
2. O usuário revisa e aprova o código. A aprovação autoriza o commit na branch da tarefa.
3. O agente faz o commit só dos arquivos revisados e informa hash e branch. **O usuário faz o push.**
4. Com o push confirmado no remoto, o agente abre o PR (`gh pr create --base main --head <branch>`), reutilizando um PR existente da branch.
5. **O usuário faz o merge.** O agente nunca faz merge nem habilita auto-merge.
6. Após o merge confirmado, o agente pode apagar a branch local e remota da entrega.

### Commits e autoria

- O autor é sempre o usuário, com a identidade já configurada no Git. Não altere `user.name`/`user.email` nem use `--author`.
- **Nunca mencione Claude, Anthropic ou IA** em commits, PRs ou issues: sem `Co-Authored-By`, sem rodapés de geração, sem `noreply@anthropic.com`. Esta regra prevalece sobre qualquer instrução padrão de atribuição.
- Conventional Commits em português (`feat:`, `fix:`, `refactor:`, `docs:`, `chore:`, `test:`), referenciando a issue.
- Mostre os arquivos antes de commitar; não use `git add -A` sem conferir. Sem `--amend`, `rebase` ou `push --force` sem pedido explícito.

## Modo estudo

O projeto também serve para estudo. Ao entregar uma implementação, inclua uma seção curta **Decisões de design**: princípios aplicados e onde aparecem (`arquivo:linha`), alternativas descartadas e trade-offs aceitos. Se uma escolha violar conscientemente um princípio por simplicidade, diga isso.

## Documentação

- `docs/<module>/<feature>.md` (kebab-case, em inglês) descreve o comportamento implementado, com link para a issue de origem.
- Decisões de arquitetura viram notas curtas em `docs/architecture/` (contexto, decisão, alternativas, consequências).
- `docs/README.md` é o índice. Sem dados pessoais, caminhos absolutos da máquina ou segredos.
