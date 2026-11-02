# Criação e redefinição do usuário

Origem: [#35](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/35). Decisão geral em [web-hosting.md](../architecture/web-hosting.md).

A aplicação tem um único usuário, criado por linha de comando no computador do serviço. A aplicação web nunca oferece cadastro: assim, ninguém da rede consegue se cadastrar antes do dono.

**Estado:** os comandos usam o PostgreSQL configurado em `ConnectionStrings__Finance`
([#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60)); sem a conexão, ou com credenciais
erradas, terminam com código 1 e uma mensagem sem a senha. Os scripts de instalação nativa abaixo são legado e
saem na [#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59), que documenta a execução
via Compose, com terminal interativo.

## Comandos

```
Ags.Finance.Web user create
Ags.Finance.Web user reset-password
```

Os comandos não sobem o servidor web: conectam ao PostgreSQL (`ConnectionStrings__Finance`), aplicam as migrations do módulo Identity, fazem a pergunta e saem. Podem rodar com o serviço ligado.

| Comando | O que faz |
| --- | --- |
| `user create` | Pede nome de usuário, senha e confirmação (sem eco) e grava o usuário. Recusa se já existe um usuário e indica `user reset-password`. |
| `user reset-password` | Pede a nova senha e a confirmação. Grava o novo hash, encerra as sessões abertas e remove qualquer bloqueio por tentativas erradas. Recusa se não há usuário e indica `user create`. |

Senha curta (menos de 8 caracteres) ou confirmação diferente são recusadas e a pergunta se repete. Se a entrada acaba antes (por exemplo, `Ctrl+D`), o comando cancela sem gravar nada.

### Códigos de saída

| Código | Significado |
| --- | --- |
| 0 | Concluído |
| 1 | Cancelado ou dado inválido |
| 2 | Uso incorreto (subcomando desconhecido ou argumento a mais) |
| 3 | `user create`: já existe um usuário |
| 4 | `user reset-password`: não há usuário |

Os scripts de instalação chamam `user create` a cada execução e tratam o código 3 como normal (instalação já tinha usuário). Veja [service-installation.md](../web/service-installation.md).

### Pasta de dados

O comando usa a mesma regra de resolução da aplicação: `DataDirectory` por variável de ambiente, argumento de linha de comando ou `appsettings.json`; sem isso, a pasta local do usuário que executa. Os scripts de instalação definem `DataDirectory` com a pasta do serviço, para o banco ficar no lugar e com o dono certos. Para rodar à mão depois da instalação:

```
# Linux
sudo runuser -u ags-finance -- env DataDirectory=/var/lib/ags-finance /opt/ags-finance/Ags.Finance.Web user reset-password

# Windows (PowerShell como administrador)
$env:DataDirectory = "$env:ProgramData\AgsFinance"; & "$env:ProgramFiles\AgsFinance\Ags.Finance.Web.exe" user reset-password
```

## Persistência

Tabela `identity.users` (migration `Initial` do módulo), com `Id`, `ExternalId`, `CreatedAt`, `UpdatedAt` e:

| Coluna | Uso |
| --- | --- |
| `UserName` | Nome de usuário, sem espaços nas pontas; comparado sem diferenciar maiúsculas |
| `PasswordHash` | Hash PBKDF2 do `PasswordHasher` do ASP.NET Core Identity, com sal e custo no próprio valor. A senha nunca é gravada |
| `SecurityStamp` | Muda quando a senha muda; a sessão (login, [#36](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/36)) guarda o carimbo e deixa de valer quando ele muda |
| `FailedSignInCount`, `LockoutEnd` | Bloqueio temporário após tentativas erradas (login) |

## Regras

- Senha com no mínimo 8 caracteres (`PasswordPolicy`, no Domain).
- Um único usuário; `user create` verifica antes de gravar.
- Os comandos não registram nada em log e nunca imprimem a senha.

## Módulo

`Ags.Finance.Modules.Identity`: `Domain/User`, `Domain/PasswordPolicy`; casos de uso `CreateUser`, `ResetPassword` e `UserExists` em `Application/`; `Contracts/ICreateUser`, `IResetPassword` e `IUserExists`, usados pelos comandos em `Ags.Finance.Web/Commands/`. Não há endpoint HTTP nem componente que use `ICreateUser` ou `IResetPassword` (um teste de arquitetura garante isso).

## Login

A orientação "rode `user create` no computador do serviço" na tela de login (sem usuário cadastrado) e o bloqueio das páginas de dados sem sessão estão em [login.md](login.md) ([#36](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/36)).
