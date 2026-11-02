# Hospedagem web, serviço e segurança

## Contexto

A primeira versão foi pensada como app desktop (Avalonia). Para usar também pelo celular, o host passou a ser web. O modelo é o do Syncthing: instalado uma vez num computador, sobe com o sistema e é acessado pelo navegador de qualquer dispositivo da mesma rede.

**Estado:** serviço implementado ([#34](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/34), veja [service-installation.md](../web/service-installation.md)). HTTPS, login e PWA ainda não.

## Decisão

- **Host:** `Ags.Finance.Web`, ASP.NET Core com Blazor Web App em render mode Interactive Server. Toda a interface é escrita em C#.
- **Serviço:** Windows Service (`UseWindowsService`) ou systemd (`UseSystemd`), com início automático. Publicação self-contained para win-x64 e linux-x64, com scripts de instalação que registram o serviço e liberam a porta no firewall.
- **Rede:** Kestrel escuta em todas as interfaces numa porta configurável (padrão 5280). Acesso só na rede local; acesso externo fica fora do escopo.
- **HTTPS:** certificado autoassinado gerado na primeira execução, com o nome do computador, `localhost` e os IPs locais. HTTP redireciona para HTTPS. Opcionalmente, um `.pfx` próprio.
- **Usuário:** um único usuário, criado pelo comando `user create` no computador do serviço, chamado pelo script de instalação. A aplicação web nunca oferece cadastro. Sem envio de e-mail, a senha é trocada logado ou pelo comando `user reset-password`.
- **Login:** cookie de autenticação, senha em hash (`PasswordHasher`, PBKDF2), bloqueio temporário após tentativas erradas, "manter conectado" opcional.
- **Celular:** PWA instalável (manifesto e service worker mínimo); os dados continuam no servidor.

## Consequências

- Qualquer dispositivo da rede alcança a porta, por isso login e HTTPS são obrigatórios desde o MVP.
- O navegador mostra um aviso de certificado até ele ser marcado como confiável em cada dispositivo.
- A conta que roda o serviço precisa ter permissão na pasta de dados e na pasta de backup.

## Alternativas consideradas

- **Desktop com Avalonia:** sem acesso pelo celular.
- **Blazor WebAssembly ou SPA (React):** mais independentes do servidor, mas com mais peças e uma API a manter, sem ganho real para uso em rede local.
- **Cadastro de usuário pela web no primeiro acesso:** alguém da rede poderia se cadastrar primeiro; descartado em favor do comando no computador do serviço.
