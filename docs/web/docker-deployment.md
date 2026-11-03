# Instalação com Docker Compose

Origem: [#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59) (distribuição) e
[#31](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/31) (backup). Decisão geral em
[web-hosting.md](../architecture/web-hosting.md); banco em [persistência PostgreSQL](../architecture/persistence-postgresql.md).

**Estado:** implementada e exercitada com Docker Engine dentro do WSL 2 (Ubuntu 24.04) num Windows 11.
O que não foi comprovado está em [Limites da validação](#limites-da-validação).

## Visão geral

| Peça | Detalhe |
| --- | --- |
| Serviços | `db` (`postgres:18.6-alpine`) e `app` (imagem `ghcr.io/adnerscarpelini/ags-controle-financeiro:<versão>`, .NET 10 em Ubuntu 24.04, usuário sem root `app`, UID 1654) |
| Rede | Só o HTTPS do `app` é publicado (porta `5280` por padrão, `AGS_PORT`). O `db` não publica porta: só o `app` o alcança pela rede interna do Compose |
| Volumes | `db-data` (dados do PostgreSQL), `app-data` (`/data`: certificado, chaves do Data Protection e futuros logos) e a pasta de backup do host (`AGS_BACKUP_DIR`, `/backups`), três destinos separados |
| Processo | `dotnet Ags.Finance.Web.dll` direto no container (PID 1), `SIGTERM` com parada graciosa (30 s), logs em stdout/stderr, `restart: unless-stopped` |
| Fuso | `TZ=America/Sao_Paulo` (`AGS_TIMEZONE`); datas de negócio seguem o fuso, a auditoria continua em UTC |
| Internet | Só é preciso para obter as imagens. Depois, a aplicação funciona na LAN sem internet |

Funciona igual em Linux (Docker Engine nativo) e no Windows com o Docker Engine instalado **dentro do WSL 2**.
Docker Desktop não é obrigatório.

## Instalação

Requisitos: Docker Engine e o plugin Compose (`docker compose version`). No Windows, WSL 2 com uma distribuição
com systemd (`/etc/wsl.conf` com `[boot]` e `systemd=true`) e o Docker Engine instalado nela.

```bash
git clone https://github.com/adnerscarpelini/ags-controle-financeiro.git
cd ags-controle-financeiro
cp .env.example .env
chmod 600 .env
```

Edite `.env`: gere duas senhas diferentes (`openssl rand -hex 24`) para `POSTGRES_PASSWORD` e `AGS_DB_PASSWORD` e
informe em `AGS_HOST_NAMES` os nomes e IPs pelos quais os dispositivos acessam o computador
(por exemplo `meu-pc,192.168.0.10`). Prepare a pasta de backup, que precisa pertencer ao UID 1654:

```bash
sudo install -d -o 1654 -g 1654 -m 0750 backups
```

Suba a instalação (usa a imagem publicada, ou constrói localmente se ela não existir; `--build` força a construção):

```bash
docker compose up -d --wait
```

Crie o único usuário. O terminal interativo é necessário (`-it`, padrão do `run`); a senha não aparece na tela e
não vai em argumento nem variável:

```bash
docker compose run --rm app user create
```

Acesse `https://<nome-ou-ip>:5280` e confie no certificado (veja [certificado HTTPS](https-certificate.md)).
Redefinir a senha: `docker compose run --rm app user reset-password` (encerra as sessões abertas).

### Credenciais, papéis e permissões

- `.env` guarda as senhas, fica fora do Git (`.gitignore`) e fora da imagem (`.dockerignore`). Mantenha `chmod 600`.
  Nada é registrado em log; a aplicação não repete a connection string em mensagens de erro.
- O primeiro start do PostgreSQL (volume vazio) executa `docker/postgres/init/10-app-role.sh`, que cria o papel
  da aplicação (`ags_app`, `LOGIN`, sem superusuário, sem `CREATEDB`/`CREATEROLE`), o banco `ags_finance` com ele
  como dono e remove o acesso público ao banco. O superusuário `postgres` só serve para manutenção.
- Papéis pertencem ao cluster, não ao banco: o `pg_dump` **não** os leva. Em uma instalação nova, o script acima os
  recria com as senhas do novo `.env` antes da restauração.

### Falhas de banco na inicialização

A aplicação espera o PostgreSQL ficar pronto (até `AGS_DB_READY_TIMEOUT` segundos, padrão 60), aplica as migrations
e só então começa a atender. Banco indisponível, credenciais recusadas ou migration com erro terminam o processo com
código 1 e uma mensagem clara no log (`docker compose logs app`), sem atender com banco parcial e sem apagar dados;
o `restart` do Compose tenta de novo.

## HTTPS e rede local

O certificado é gerado na primeira execução em `/data/certs`, com os nomes e IPs de `AGS_HOST_NAMES` (a rede interna do
container não é consultada), e reaproveitado. Ao mudar `AGS_HOST_NAMES`, recrie o container
(`docker compose up -d`): o certificado é regerado e precisa ser confiado de novo nos dispositivos. O arquivo
público para instalar nos dispositivos sai do volume: `docker compose cp app:/data/certs/ags-finance.crt .`. HTTP na mesma porta redireciona para HTTPS.

### Windows com WSL 2

O WSL precisa deixar a porta `5280` acessível ao resto da rede:

- **Rede espelhada** (Windows 11 22H2+, `networkingMode=mirrored` em `%UserProfile%\.wslconfig`): o WSL compartilha os
  IPs do Windows. Libere a porta no firewall do Windows.
- **Modo NAT** (padrão): encaminhe a porta do IP do Windows para o IP atual do WSL e libere o firewall. Em um PowerShell
  de administrador (o IP do WSL muda a cada inicialização; repita o `portproxy` depois de reiniciar):

```powershell
$wslIp = (wsl -d Ubuntu-24.04 -- hostname -I).Trim().Split(' ')[0]
netsh interface portproxy add v4tov4 listenport=5280 listenaddress=0.0.0.0 connectport=5280 connectaddress=$wslIp
New-NetFirewallRule -DisplayName 'AgsFinance 5280' -Direction Inbound -Protocol TCP -LocalPort 5280 -Action Allow -Profile Private
```

### Inicialização automática no Windows

A política `restart` do container **não** inicia o WSL. Para subir tudo depois de reiniciar o Windows, sem terminal
aberto, registre a tarefa agendada (PowerShell; use `-WhatIf` para só ver o que será feito):

```powershell
./scripts/install-wsl-autostart.ps1 -Distribution Ubuntu-24.04 -LinuxUser maria -ProjectPath /home/maria/ags-controle-financeiro
```

A tarefa roda no logon do usuário Windows, sobe o Compose e mantém a sessão do WSL viva. `-RunWithoutLogon` a move
para a inicialização do Windows, antes do logon. `-Remove` apaga a tarefa. Em modo NAT, o `portproxy` acima também
precisa ser refeito (ou prefira a rede espelhada).

## Backup e recuperação

Um backup é uma pasta em `AGS_BACKUP_DIR` com `database.dump` (`pg_dump` formato custom de **todo o banco**: todos os
schemas, migrations, dados, sequências e vínculos), `app-files.tar.gz` (certificado e chave privada, chaves do Data
Protection e demais arquivos de `/data`) e `manifest.json` (versões do PostgreSQL, do `pg_dump` e da aplicação, migrations
aplicadas e SHA-256 dos arquivos). A pasta só aparece como backup depois de completa e conferida (`pg_restore --list`);
antes disso vive em `.partial-*`, que nunca é listada nem restaurada. Os arquivos ficam acessíveis só ao dono (0600/0700):
contêm dados financeiros e a chave privada do certificado.

```bash
docker compose run --rm app backup create          # faz um backup agora
docker compose run --rm app backup list            # lista os backups
docker compose run --rm app backup verify NOME     # confere arquivos, SHA-256 e se o dump abre
docker compose stop app && docker compose run --rm app backup restore NOME
```

### Agenda, retenção e tela

- **Backup agendado:** a aplicação faz um backup por dia no horário `AGS_BACKUP_TIME` (padrão `02:00`, no fuso da aplicação).
  Se o computador estava desligado no horário, o backup devido é feito assim que a aplicação sobe; uma falha é registrada e
  repetida em 1 hora. Sem `Backup__Directory`, a agenda fica desativada.
- **Retenção:** mantém os `AGS_BACKUP_RETENTION` backups mais recentes (padrão 30). O **primeiro backup de cada mês** é marcado
  como mensal e nunca é apagado; o backup mais recente também nunca é apagado. Só pastas que são backups válidos são tocadas.
- **Tela:** em **Configurações > Backup e restauração** (`/backups`): agenda e retenção, último resultado, **Fazer backup agora**
  e a lista de backups com **Restaurar**. A restauração pela tela exige digitar o nome do backup, coloca a aplicação em
  **manutenção** (todas as requisições recebem 503 enquanto dura), faz um backup do estado atual, encerra as conexões ao banco,
  restaura numa única transação e reinicia a aplicação (o Compose a sobe de novo, já com o certificado e as chaves restaurados).
  Se falhar, a manutenção termina e o estado anterior permanece.
- **Antes de atualizar:** `docker compose run --rm app backup create --pre-update` marca o backup como pré-atualização.
- **Registro:** início, fim, sucesso ou falha e o nome do arquivo vão para o log da aplicação (`docker compose logs app`), sem
  credenciais nem conteúdo financeiro.

O ponto recuperável é o estado do **backup concluído**: não há recuperação por instante nem garantia de perda zero
entre backups. CSV é troca de dados do sistema e não substitui o backup.

### Cópia para fora da máquina

Volume ou pasta local não protegem contra a perda do computador, do disco ou da distribuição WSL. Depois de cada backup,
copie a pasta para outro dispositivo ou nuvem **a partir do host** (não há sincronizador nem API de nuvem no container):

```bash
sudo rsync -a backups/ /mnt/disco-externo/ags-backups/        # Linux ou WSL, com o disco montado
```

No WSL, a pasta pode ficar em `/mnt/d/ags-backups` (outro disco do Windows) se pertencer ao UID 1654; ou use `rsync`/`robocopy`
do Windows sobre `\\wsl$\<distro>\...`. Não sincronize a pasta de dados ativa do PostgreSQL (`db-data`).

### Recuperar em uma instalação nova

1. Máquina nova: instale Docker/Compose, clone o repositório e crie o `.env` (senhas novas servem; `AGS_HOST_NAMES` do novo computador).
2. Copie a pasta do backup para `AGS_BACKUP_DIR` (dono UID 1654) e suba só o banco: `docker compose up -d --wait db`
   (o script de inicialização cria o papel e o banco vazios).
3. Restaure e suba a aplicação:

```bash
docker compose run --rm app backup restore NOME
docker compose up -d --wait app
```

O restore confere o pacote e as versões, exige acesso exclusivo ao banco (recusa se há outras conexões, a menos que se use
`--force`), **faz um backup do estado atual** (`pre-restore`) e restaura numa única transação: apaga os schemas atuais e
recria os do backup, então um erro desfaz tudo e nunca anuncia sucesso parcial. Depois restaura os arquivos da aplicação e
aplica migrations que faltarem. O backup e o servidor precisam ter a mesma versão principal do PostgreSQL.

### Ensaio de recuperação

`scripts/rehearse-recovery.sh` automatiza o ensaio completo, com dados fictícios e projetos Compose descartáveis
(`ags-rehearsal-a/b`): usuário e registros com vínculos → backup → instalação nova **vazia e sem o volume original** →
restauração → login, registros, vínculos, totais e sequências conferidos, sessão antiga ainda válida, certificado idêntico.
As definições das chaves estrangeiras são comparadas antes e depois da restauração.
Vínculos dentro do schema de um módulo são permitidos; FKs entre schemas são rejeitadas.
Rode-o a partir da raiz, em Linux ou no WSL: `scripts/rehearse-recovery.sh`.

## Atualização

1. Faça um backup e copie-o para fora: `docker compose run --rm app backup create --pre-update` (precisa terminar antes de atualizar).
2. Troque `AGS_VERSION` no `.env` para a nova versão publicada (por exemplo `0.2.0`) e rode `docker compose pull`.
3. `docker compose up -d`: o novo container aplica as migrations pendentes e preserva os dados.

Voltar à imagem anterior **não desfaz** migrations do banco: para voltar atrás, restaure o backup anterior à atualização.
A atualização de versão principal do PostgreSQL (por exemplo 18 → 19) tem procedimento próprio: backup, nova instalação com a
nova imagem do banco e restauração. Imagens são versionadas por tag; releases publicam em GHCR (`vX.Y.Z`).

## Reset de desenvolvimento (restrito)

Vale só para os dados descartáveis **deste projeto** em máquina de desenvolvimento, nunca para uma instalação com dados reais:

```bash
docker compose down -v        # apaga APENAS os volumes db-data e app-data deste Compose (projeto ags-finance)
```

Confira o projeto (`docker compose ls`) antes: `-v` remove os volumes do projeto atual e nada de outros projetos. A pasta de
backup (`AGS_BACKUP_DIR`) não é apagada. Para limpar o SQLite legado do `dotnet run`, use `scripts/clean-legacy-dev-data.ps1`.

## Desenvolvimento

`dotnet run --project src/Ags.Finance.Web` continua funcionando com um PostgreSQL configurado
(`ConnectionStrings__Finance`; veja o [README](../../README.md)). `DataDirectory` aponta para a pasta de arquivos da
aplicação (padrão: pasta local do usuário). Testes relacionais: [persistência PostgreSQL](../architecture/persistence-postgresql.md#testes-relacionais).

## Limites da validação

Validado: construção da imagem, subida do Compose, papéis do banco, criação do usuário, login, recriação dos containers
mantendo certificado e backups, backup agendado feito pela própria aplicação, parada graciosa (código 0 em 1 s), falha clara com credencial errada e com banco indisponível,
fuso `America/Sao_Paulo` (01/01/2027 02:30 UTC = 31/12/2026 23:30), backup, verificação, restauração em instalação vazia
e continuidade da sessão.

**Não comprovado** nesta entrega e que exige teste em campo: reinício real do Windows com a tarefa de inicialização
automática, acesso do celular pela LAN (firewall e `portproxy`/rede espelhada), confiança do certificado nos dispositivos,
teste de internet desligada com as imagens já obtidas e Linux nativo (apenas o Docker Engine no WSL foi usado).
