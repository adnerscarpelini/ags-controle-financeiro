# CLAUDE.md

Instruções para agentes de IA que trabalham neste repositório. Leia este arquivo inteiro antes de planejar, implementar, revisar ou documentar qualquer coisa.

## O projeto

`ags-controle-financeiro` é uma aplicação web pessoal de controle financeiro, acessada pelo navegador do computador e do celular na **rede local**. Tem um único usuário e funciona sem internet depois de obtidas as imagens de distribuição.

- **Destino aprovado:** .NET 10, C#, ASP.NET Core com Blazor Web App (Interactive Server), EF Core com **PostgreSQL como único banco**, xUnit e **Docker Compose**. Mesma imagem Linux no Docker Engine nativo ou no Windows via WSL 2; Docker Desktop não é obrigatório.
- **Estado do código:** persistência **PostgreSQL** (#60) e distribuição por **Docker Compose** (#59), com backup e recuperação (#31), no lote 06. Serviço nativo e SQLite foram removidos. Guia em `docs/web/docker-deployment.md`. Reinício real do Windows e celular na LAN ainda precisam de teste em campo; não afirmar que foram comprovados.
- **Dados de desenvolvimento:** o usuário autorizou descartar os dados deste projeto, recriar migrations e remover infraestrutura obsoleta na #60/#59 (o SQLite já foi removido). Não desenvolver migração SQLite → PostgreSQL nem suporte duplo. Conferir alvos de limpeza; a autorização não alcança outros projetos nem apaga o histórico Git.
- **Troca de arquivos:** somente importação/exportação de cadastros e lançamentos por **CSV no layout oficial e versionado do sistema** (#28, #29, #61, #62). PDF, OFX, XLS/XLSX, OCR, layouts bancários e migração da antiga planilha não fazem parte do produto. CSV não substitui backup.
- **Repositório público:** nunca versione dados financeiros reais, nomes de estabelecimentos reais do usuário, extratos, faturas, planilhas ou o arquivo do banco. Testes e exemplos usam somente dados fictícios.
- **Idioma:** converse, escreva issues, PRs, commits e documentação em **português**. Escreva **todo o código em inglês** (tipos, propriedades, métodos, variáveis, tabelas, colunas, testes), seguindo as convenções oficiais da Microsoft para C#/.NET. Textos exibidos na interface ficam em português.

## Arquitetura

Monólito modular com Clean Architecture dentro de cada módulo. A fonte de verdade é `docs/architecture/`; em caso de divergência, siga a documentação e aponte a diferença.

```
src/
  Ags.Finance.Web/                # host ASP.NET Core + Blazor: composition root, páginas, componentes, execução e backup
  Ags.Finance.SharedKernel/       # somente Money e ReferenceMonth
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

Módulos: `Attachments`, `Identity`, `Accounts`, `Transactions`, `Invoices`, `Reimbursements`, `Preferences`, `Reports`, `Imports` e `Diagnostics`. Responsabilidades e dependências permitidas estão em `docs/architecture/module-structure.md`.

### Regras de fronteira

- `internal` e `sealed` por padrão. Só são `public` a classe `DependencyInjection` e os tipos em `Contracts`. Testes acessam tipos internos via `InternalsVisibleTo`.
- Um módulo só referencia outro pelos `Contracts`, numa direção única e sem ciclos. Uma nova dependência entre módulos precisa ser registrada em `docs/architecture/`.
- `Contracts` expõe DTOs e interfaces pequenas, com o `ExternalId` (`Guid`). Nunca exponha entidades ou o `Id` interno.
- `Domain` não depende de EF Core, ASP.NET Core, Blazor nem de outras pastas. `Application` depende de `Domain` e de `Contracts`.
- Páginas e componentes Blazor chamam casos de uso pelos `Contracts` dos módulos, nunca a `Infrastructure`. Regra de negócio não fica em componente.
- O `SharedKernel` contém apenas `Money` e `ReferenceMonth`. Não adicione nada a ele sem uma decisão registrada.
- Não adote CQRS, MediatR, Repository genérico, Unit of Work extra ou mensageria sem um problema concreto discutido antes.

### Persistência e recuperação (decisão PostgreSQL)

- Um banco PostgreSQL, com **schema, DbContext e histórico de migrations próprios por módulo** (`identity`, `accounts`, `transactions`, `invoices`, `reimbursements`, `preferences`, `attachments`, `diagnostics`), conforme [a decisão de persistência](docs/architecture/persistence-postgresql.md). A conexão vem de `ConnectionStrings__Finance` e nunca é versionada nem registrada em log.
- Sem FK, JOIN ou SQL que cruze tabelas de módulos diferentes. Uma referência a outro módulo guarda o `ExternalId` dele.
- Toda entidade com tabela própria tem `Id` (`long`, chave primária), `ExternalId` (`Guid`, gerado no domínio com `Guid.CreateVersion7()`, índice único), `CreatedAt` e `UpdatedAt` (`DateTimeOffset` UTC, vindos de `TimeProvider`, preenchidos por um interceptor do módulo).
- Mapeamentos com `IEntityTypeConfiguration<T>` na `Infrastructure`; o domínio não tem atributos de persistência.
- Valores monetários: `decimal` no código (`Money`), centavos em `BIGINT` no PostgreSQL. Nunca `float` ou `double`. Preservar `ReferenceMonth` como inteiro `yyyyMM`.
- O PostgreSQL tem volume persistente próprio. `DataDirectory` fica para certificados, chaves e arquivos da aplicação. Não manter dados ativos em pasta sincronizada.
- Anexos originais ficam em pasta própria (`Attachments__Directory`, `AGS_ATTACHMENTS_DIR`), separada de DataDirectory e backup; metadados no schema `attachments`. PDF, JPEG, PNG e WebP até 20 MB, lixeira de 30 dias. Ver `docs/architecture/attachments-storage.md`.
- Backup de anexos: manifesto v2, índice `attachments.json` por pacote e cópia incremental única em `<backup>/attachments/`, incluindo lixeira; copiar a pasta de backup inteira para fora da máquina. Backups v1 continuam verificáveis e restauráveis. Arquivos faltantes são copiados antes da restauração SQL; o ZIP de anexos não é reimportado nem substitui o backup.
- Backup sob demanda com `pg_dump` e recuperação com `pg_restore`, incluindo preparação de papéis/permissões e arquivos associados. Validar recuperação em instalação vazia e guardar cópia fora da máquina. O backup é um serviço da aplicação (`Ags.Finance.Web/Backup`): comandos `backup create|list|verify|restore`, agenda diária, retenção (30 + mensal preservado) e tela `/backups` com restauração em modo de manutenção (#59/#31). Volumes e CSV não equivalem a backup de desastre.

### Regras de negócio centrais

Leia as issues antes de implementar. As regras mais sensíveis são:

- **ReferenceMonth:** no débito, PIX ou dinheiro, é o mês da data. No cartão, é o mês do vencimento da fatura em que a compra cai, conforme o dia de fechamento do cartão. Fora do cartão não é editável e acompanha a data; no cartão a tela permite escolher outra fatura Aberta. Se a calculada estiver protegida (inclusive pelo corte, mesmo sem registro), sugerir a próxima disponível e avisar. Edições de detalhes preservam a fatura; alterar data/cartão recalcula e é bloqueado em parcelas antecipadas. Alterar o calendário do cartão não recalcula histórico. CSV conserva exatamente o mês do arquivo (#89).
- **Parcelas:** uma compra em N vezes gera N lançamentos em meses de referência consecutivos. A diferença de centavos vai para a primeira parcela.
- **Pagamento de fatura não é despesa:** é um `InvoicePayment`. Despesas já estão lançadas item a item.
- **Faturas:** somente **Aberta, Fechada e Paga** (#50). Fechamento automático pelo dia, com opção manual. Compras em Fechada/Paga não podem ser editadas, excluídas ou movidas; destino de compra também precisa estar Aberto. Reabertura manual com pagamento confirmado (inclusive Aberta com pagamento parcial/antecipado) desfaz todos os pagamentos, restaura os débitos nas contas originais e mantém histórico, anexos, cashback e descontos; suprime novo fechamento automático até fechamento manual (#93). Saldo zero/crédito não marca Paga automaticamente: exigir confirmação explícita de quitação.
- **Pagamentos:** totais ou parciais, antecipados ou após fechamento. Pagamentos são desfeitos em conjunto ao reabrir a fatura; pagamentos com UndoneAt ficam fora de todas as somas, inclusive Reports (#93). Conta vinculada ao cartão vem pré-selecionada, mas pode ser trocada; debitar apenas a escolhida, sem segunda despesa (#19/#51). Antecipação não fecha fatura Aberta por si só.
- **Ajuste de fatura:** em Aberta/Fechada, informar total correto e criar lançamento da diferença, afetando despesas do mês. É exceção específica ao bloqueio da Fechada, sem reabrir, editar compras ou alterar pagamentos; Paga bloqueia ajuste (#52).
- **Recorrência:** editar pergunta o alcance ao salvar: Somente esta (padrão), Esta e as futuras em aberto ou Todas (incluindo Pagas/Recebidas); os alcances coletivos atualizam a definição sem dividir a série. Excluir mantém Somente esta, Esta e as seguintes ou Todas. A baixa manual é por ocorrência e não muda a série (#92/#16/#17/#56).
- **Efetivação fora do cartão:** chave Pago/Recebido ligada por padrão; desligada cria Pendente. Na recorrência e na repetição finita, a primeira ocorrência segue a chave e as seguintes nascem Pendentes. Pagar/Receber permite alterar valor, data e conta apenas da ocorrência e é idempotente; desligar a chave na edição desfaz a baixa e restaura o saldo. Passar a data apenas torna a pendência atrasada. Cartões não têm baixa manual. Separar saldo efetivo, previsão e orçamento mensal (#88, #56–#58).
- **Comportamento do mês:** no Dashboard, os cards de comportamento e as despesas por categoria somam pela data da despesa, em qualquer forma de pagamento; a compra parcelada conta inteira no mês da compra. O orçamento do mês continua pelo mês de referência. Ajuste de fatura conta no mês de referência dele. Ver `docs/reports/monthly-behavior.md` (#117).
- **Planejamento mensal:** Transactions guarda o planejamento e as metas; Reports compara somente por Contracts. Renda pode ser zero, nunca negativa; metas podem exceder o orçamento. Arredondar o orçamento em centavos e obter a economia pela diferença. Receitas reais contam somente Recebidas. `/planejamento` abre o orçamento mensal; a visão de 19 meses fica em `/relatorios/visao-19-meses` (#94).
- **Saldo negativo:** transferências e pagamentos de fatura podem deixar a conta corrente negativa (#46/#51); não bloquear essas operações apenas por insuficiência de saldo.
- **Transferências:** origem é conta corrente ativa; destino, qualquer outra conta ativa. A recorrente segue as despesas: a primeira ocorrência obedece à chave, as seguintes nascem pendentes e só movimentam as contas ao confirmar. Pendentes entram no saldo previsto e nas pendências, fora de receitas, despesas e orçamento (#124).
- **Reembolsos:** pessoa e percentual são informados na despesa e na série; o percentual fica gravado. Pendente é derivado na leitura; só o recebimento é gravado. Situações: Pendente e Recebido; receber menos que o esperado encerra o item. Cada efetivação cria uma receita por pessoa, e recebimento pode ser desfeito. Despesa recebida não muda valor, tipo, pessoa ou percentual nem é excluída (#78–#84).
- **Arquivamento:** contas/cartões podem ser arquivados e desarquivados, saindo dos cadastros padrão e novas seleções. Histórico, relatórios e faturas pendentes continuam visíveis (#53). Bancos têm nome único e podem ser editados; sem conta vinculada são excluídos e, com conta, arquivados (PR #110). Não estender essa regra a outros cadastros sem issue.

As regras aprovadas ainda sem implementação permanecem nas issues. Consulte [as decisões de consistência financeira](docs/architecture/financial-consistency.md) para distinguir conceitos; não criar estados/regras adicionais para cobrir dúvidas ainda abertas.

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

Tema **escuro por padrão** (claro e automático nas preferências), no estilo limpo de Todoist, Notion e aplicativos da Apple: fundo neutro, seções em cards suaves, com o verde "dinheiro" só na ação principal, no foco e nas receitas (#112). Os tokens ficam como variáveis CSS num único arquivo; não use cores soltas nos componentes. A interface é responsiva: funciona no computador e no celular, e pode ser instalada como PWA. Detalhes em `docs/architecture/ui-design.md`.

| Token | Claro | Escuro | Uso |
| --- | --- | --- | --- |
| `Background` | `#FFFFFF` | `#141415` | fundo da janela |
| `Sidebar` | `#F7F7F6` | `#1A1A1C` | menu lateral |
| `Card` | `#F7F7F6` | `#1B1B1D` | cards suaves de seções, totais e entidades |
| `Surface` | `#FEFEFE` | `#222224` | menus e janelas |
| `SurfaceRaised` | `#F2F2F1` | `#2C2C2F` | superfície neutra sólida |
| `Border` | `#E8E8E7` | `#2C2C2E` | linhas finas e divisórias |
| `BorderStrong` | `#D1D1CF` | `#48484A` | bordas tracejadas e controles |
| `TextPrimary` | `#1D1D1F` | `#F5F5F7` | texto principal e despesas |
| `TextSecondary` | `#6A6A6F` | `#A1A1A6` | rótulos e texto secundário |
| `Accent` | `#0B7A55` | `#30C48D` | ação principal, foco e item selecionado |
| `AccentHover` | `#09684A` | `#4AD6A1` | hover do accent |
| `Income` | `#0A7450` | `#4AD6A2` | receitas |
| `Expense` | `#C4372E` | `#FF8177` | atraso, excesso e erro |
| `Warning` | `#946200` | `#E5B13A` | pendências e alertas |

- Componentes do **MudBlazor** ([decisão](docs/architecture/ui-library.md)), com `AppTheme` espelhando os tokens, e CSS próprio só com os tokens. O tema é preferência do usuário (escuro, claro ou automático; ver `docs/preferences/settings.md`), mas o escuro é o padrão. Cada token é `light-dark(claro, escuro)` e todo texto mantém contraste mínimo 4,5:1 nos dois temas.
- Nada carregado de CDN: fontes, scripts e ícones são servidos pela própria aplicação.
- Fonte Inter (servida localmente), números com dígitos tabulares e alinhados à direita. Valores em `R$ 1.234,56`, datas em `dd/MM/yyyy` (cultura `pt-BR`).
- Cantos de 8 em controles, 12 em cards e 14 em janelas, espaçamento em múltiplos de 4, sombras leves só em menus e janelas. Botões sem caixa alta e sem sombra; 32 px no computador e 44 px no celular. Seções, totais e entidades ficam em cards suaves (`Card`), sem borda e sem sombra; nunca card dentro de card.
- **Botões sempre à direita:** em telas, janelas e formulários, no topo ou no rodapé, as ações ficam alinhadas à direita (`page-head-actions`, `form-actions`, `mud-dialog-actions`).
- **Nenhum controle com aparência do sistema:** lista de select, calendário, seletor de mês, sugestões, validação e dicas abertos seguem os tokens (`wwwroot/js/pickers.js`). A única exceção é a janela de escolha de arquivos.
- O verde é a cor de marca e das receitas. Em toda a aplicação, a cor do valor vem de `ValueColor`: despesa em vermelho, receita em verde, saldo pelo sinal e zero em cor neutra. Saldo restante de fatura fica vermelho e crédito verde; pagamento de fatura e transferência são neutros. Não use cor como único sinal: valores negativos também levam o sinal.
- Cores de banco só no texto, em peso normal: Itaú `#FB923C`, Nubank `#A78BFA`, Bradesco `#F87171`.

## Segurança

- Toda página e endpoint exige login, exceto login, o endpoint necessário de entrada (`POST /account/sign-in`) e arquivos estáticos. Não ampliar exceções sem necessidade registrada.
- O único usuário é criado pelo comando `user create`, executado pelo Compose com terminal interativo (`docker compose run --rm app user create`). A aplicação web nunca oferece cadastro de usuário; a senha é trocada logado ou pelo comando `user reset-password`.
- Senhas só em hash (`PasswordHasher` do ASP.NET Core Identity). Bloqueio temporário após tentativas erradas.
- HTTPS com certificado local gerado na primeira execução; HTTP redireciona para HTTPS.
- Não registre senhas, cookies ou dados financeiros em logs.
- Erros inesperados vão para `diagnostics.error_records` por `ILogger` (`Error`/`Critical` com exceção), sem a mensagem da exceção. Um `catch` que transforma falha inesperada em mensagem registra a exceção; erro esperado não é registrado (#107).

## Testes e validação

- Teste regras de domínio e casos de uso pelo comportamento observável: caminho feliz, falhas e limites (fim de mês, dia de fechamento, centavos de parcelas).
- Testes relacionais usam PostgreSQL real isolado (`PostgresTestDatabase`, variável `AGS_TEST_POSTGRES`; veja `tests/postgres/compose.yaml`). Não use SQLite ou provider em memória como substituto. `dotnet test` exige esse servidor.
- Mantenha os testes de arquitetura (`Ags.Finance.ArchitectureTests`) cobrindo as regras de fronteira. Uma nova regra de fronteira ganha um teste.
- Antes de entregar, execute: `dotnet build Ags.Finance.slnx` (zero avisos), `dotnet test Ags.Finance.slnx` e `dotnet format Ags.Finance.slnx --verify-no-changes`.
- Nunca declare um teste aprovado sem tê-lo executado. Relate o que foi executado e as limitações.

## Fluxo de trabalho

### Issues

- Os requisitos vivem em GitHub Issues. Não crie specs ou arquivos de requisitos no repositório; `docs/` descreve comportamento implementado e decisões de arquitetura. Decisões aprovadas ainda não implementadas devem ter estado explícito e links para as issues; não duplicar seus critérios como uma nova especificação.
- Título: `[<Module>] <ação no infinitivo>` (ex.: `[Transactions] Lançar compra parcelada`). Tarefas sem módulo usam `[Transversal]`.
- Corpo segue os templates de `.github/ISSUE_TEMPLATE/`. Critérios de aceite no formato *Dado / Quando / Então*, incluindo falhas e limites.
- Labels: uma de tipo (`feature`, `bug`, `refactor`, `architecture`, `maintenance`) e uma `module:<nome>` por módulo afetado.
- Issues pequenas (INVEST): uma funcionalidade por issue, que caiba num PR revisável. Requisito novo descoberto durante o trabalho vira outra issue, não escopo extra.
- Funcionalidade ou regra nova sem issue: primeiro analise, discuta e proponha o rascunho; só registre após aprovação explícita do usuário.

### Branches e pull requests

- GitHub Flow. `main` é protegida: sem push direto, sem force push, histórico linear. O merge é feito com **Rebase and merge**, para manter na `main` um commit por issue.
- O trabalho é organizado em **lotes**: um grupo de issues relacionadas, desenvolvidas numa mesma branch e entregues num único PR. A branch do lote sai da `main` atualizada: `feature/lote-NN-<nome>` (ex.: `feature/lote-07-lancamentos`).
- Uma issue avulsa, fora de lote, usa `<tipo>/<N>-<descricao-curta>` (`feature`, `fix`, `refactor`, `docs`, `chore`).
- **Um commit por issue**, na ordem do lote, cada um compilando e com os testes passando.
- Título do PR em Conventional Commits, em português: `feat: lote 07 — lançamentos (#11, #12, #13)`; para issue avulsa, `feat: lança compra parcelada (#13)`.
- Descrição do PR com as seções do template: Resumo, Decisões de design (uma subseção por issue), QA, Documentação e uma linha `Closes #N` para cada issue.

### Divisão de responsabilidades

1. O agente implementa as issues do lote em ordem e, para cada uma, executa as validações e faz o commit.
2. O agente faz o push da branch e abre o PR para a `main`, reutilizando um PR existente da branch, com as evidências e as limitações.
3. **O usuário revisa o PR e faz o merge.** O agente nunca faz merge nem habilita auto-merge.
4. Se o usuário pedir mudanças no PR, o agente corrige num commit novo referenciando a issue (`fix: ... (#N)`) e faz o push de novo, sem reescrever o histórico da branch.
5. Após o merge confirmado, o agente pode apagar a branch local e remota da entrega.

### Commits e autoria

- O autor é sempre o usuário, com a identidade já configurada no Git. Não altere `user.name`/`user.email` nem use `--author`.
- **Nunca mencione Claude, Anthropic ou IA** em commits, PRs ou issues: sem `Co-Authored-By`, sem rodapés de geração, sem `noreply@anthropic.com`. Esta regra prevalece sobre qualquer instrução padrão de atribuição.
- Conventional Commits em português (`feat:`, `fix:`, `refactor:`, `docs:`, `chore:`, `test:`), referenciando a issue.
- Confira os arquivos antes de commitar; não use `git add -A` sem conferir. Sem `--amend`, `rebase` ou `push --force` sem pedido explícito.

## Modo estudo

O projeto também serve para estudo. Ao entregar uma implementação, inclua uma seção curta **Decisões de design**: princípios aplicados e onde aparecem (`arquivo:linha`), alternativas descartadas e trade-offs aceitos. Se uma escolha violar conscientemente um princípio por simplicidade, diga isso.

## Documentação

- `docs/<module>/<feature>.md` (kebab-case, em inglês) descreve o comportamento implementado, com link para a issue de origem.
- Decisões de arquitetura viram notas curtas em `docs/architecture/` (contexto, decisão, alternativas, consequências).
- `docs/README.md` é o índice. Sem dados pessoais, caminhos absolutos da máquina ou segredos.
