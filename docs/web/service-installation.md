# Instalação como serviço

**Código atual/legado:** esta entrega nativa (#34) será substituída pelo Docker Compose
na [#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59), após a migração
PostgreSQL [#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60).
Os comandos abaixo ainda correspondem aos scripts existentes; não são o novo caminho
de distribuição nem uma instrução para preservar dados SQLite na transição autorizada.

Origem: [#34](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/34). Decisão geral em [web-hosting.md](../architecture/web-hosting.md).

## Publicar

```
dotnet publish src/Ags.Finance.Web -c Release -r win-x64 --self-contained -o artifacts/win-x64
dotnet publish src/Ags.Finance.Web -c Release -r linux-x64 --self-contained -o artifacts/linux-x64
```

## Instalar e desinstalar

| Sistema | Instalar | Desinstalar |
| --- | --- | --- |
| Windows (PowerShell como administrador) | `scripts\install-windows.ps1 [-SourceDir pasta] [-Port 5280] [-FirewallProfile Private,Domain]` | `scripts\uninstall-windows.ps1` |
| Linux (sudo) | `scripts/install-linux.sh [--source pasta] [--port 5280]` | `scripts/uninstall-linux.sh` |

Os scripts podem ser executados de novo para atualizar os binários ou trocar a porta. Ao final mostram os endereços de acesso.

| | Windows | Linux |
| --- | --- | --- |
| Serviço | `AgsFinance`, início automático, conta virtual `NT SERVICE\AgsFinance` | `ags-finance.service`, habilitado, usuário de sistema `ags-finance` |
| Binários | `%ProgramFiles%\AgsFinance` | `/opt/ags-finance` |
| Dados | `%ProgramData%\AgsFinance` (só administradores e a conta do serviço) | `/var/lib/ags-finance` (modo 750) |
| Firewall | regra `AGS Finance` na porta, perfis Privado e Domínio | `ufw allow <porta>/tcp`, se o ufw estiver ativo |
| Log | Event Log (Application, fonte `AgsFinance`) | journal (`journalctl -u ags-finance`) |

## Comportamento

- **Porta:** `Hosting:Port` em `appsettings.json` (padrão 5280). Os scripts a definem pela variável `Hosting__Port` do serviço. O Kestrel escuta em todas as interfaces, em HTTPS (veja [https-certificate.md](https-certificate.md)).
- **Usuário:** os scripts chamam `Ags.Finance.Web user create` a cada execução, com a pasta de dados do serviço, e tratam o código de saída 3 (usuário já existe) como normal. Veja [user-creation.md](../identity/user-creation.md).
- **Porta em uso:** a aplicação registra o erro crítico e sai com código 1. No Windows não há ação de reinício, então o serviço fica parado. No Linux, `Restart=on-failure` com no máximo 5 tentativas em 5 minutos.
- **Desinstalação:** remove serviço, regra de firewall e binários; a pasta de dados e o usuário `ags-finance` ficam.
