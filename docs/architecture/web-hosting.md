# Hospedagem web, serviço e segurança

## Contexto

A primeira versão foi pensada como app desktop (Avalonia). Para usar também pelo celular, o host passou a ser web. O modelo é o do Syncthing: instalado uma vez num computador, sobe com o sistema e é acessado pelo navegador de qualquer dispositivo da mesma rede.

**Estado:** serviço ([#34](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/34), veja [service-installation.md](../web/service-installation.md)), HTTPS ([#37](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/37), veja [https-certificate.md](../web/https-certificate.md)) e criação do usuário ([#35](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/35), veja [user-creation.md](../identity/user-creation.md)) implementados. Login e PWA ainda não.

## Decisão

- **Host:** `Ags.Finance.Web`, ASP.NET Core com Blazor Web App em render mode Interactive Server. Toda a interface é escrita em C#.
- **Serviço:** Windows Service (`UseWindowsService`) ou systemd (`UseSystemd`), com início automático. Publicação self-contained para win-x64 e linux-x64, com scripts de instalação que registram o serviço e liberam a porta no firewall.
- **Rede:** Kestrel escuta em todas as interfaces numa porta configurável (padrão 5280). Acesso só na rede local; acesso externo fica fora do escopo.
- **HTTPS:** certificado autoassinado gerado na primeira execução, com o nome do computador, `localhost` e os IPs locais. HTTP redireciona para HTTPS. Opcionalmente, um `.pfx` próprio.
  - **Mesma porta para HTTP e HTTPS:** um middleware de conexão do Kestrel espia o primeiro byte; `0x16` (início do handshake TLS) segue para o HTTPS e qualquer outro pedido recebe um `307` para a URL HTTPS equivalente. Quem digita `http://ip:5280` chega ao site e o firewall abre uma única porta.
- **Usuário:** um único usuário, criado pelo comando `user create` no computador do serviço, chamado pelo script de instalação. A aplicação web nunca oferece cadastro. Sem envio de e-mail, a senha é trocada logado ou pelo comando `user reset-password`.
- **Login:** cookie de autenticação, senha em hash (`PasswordHasher`, PBKDF2), bloqueio temporário após tentativas erradas, "manter conectado" opcional.
- **Celular:** PWA instalável (manifesto e service worker mínimo); os dados continuam no servidor.

## Consequências

- Qualquer dispositivo da rede alcança a porta, por isso login e HTTPS são obrigatórios desde o MVP.
- O navegador mostra um aviso de certificado até ele ser marcado como confiável em cada dispositivo.
- A conta que roda o serviço precisa ter permissão na pasta de dados e na pasta de backup.

## Alternativas consideradas

- **HTTP numa segunda porta só para redirecionar:** mais simples, mas exige uma segunda regra de firewall e quem digita `http://ip:5280` recebe erro de conexão.
- **Container Docker:** discutido; não é o caminho principal porque no Windows exige o Docker Desktop (sobe no login, não no boot) e dentro do container o certificado não enxerga o IP e o nome do computador. Fica como possível distribuição adicional para Linux.
- **Desktop com Avalonia:** sem acesso pelo celular.
- **Blazor WebAssembly ou SPA (React):** mais independentes do servidor, mas com mais peças e uma API a manter, sem ganho real para uso em rede local.
- **Cadastro de usuário pela web no primeiro acesso:** alguém da rede poderia se cadastrar primeiro; descartado em favor do comando no computador do serviço.
