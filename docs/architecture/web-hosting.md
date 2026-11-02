# Hospedagem web, distribuição e segurança

## Contexto e estado

A aplicação é um servidor web pessoal, para computador e celular na rede local.
O host ASP.NET Core/Blazor, HTTPS e autenticação já estão implementados
([#34](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/34),
[#35](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/35),
[#36](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/36),
[#37](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/37)).
A PWA ainda está pendente na [#38](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/38).

**Implementado:** Docker Compose com aplicação e PostgreSQL, na ordem
[#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60) →
[#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59), com backup e recuperação na
[#31](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/31). O guia de instalação e operação é
[docker-deployment.md](../web/docker-deployment.md). O serviço nativo (Windows Service/systemd) e seus scripts foram removidos;
não há dois caminhos de instalação.

## Decisão

- Um container Linux da aplicação e outro PostgreSQL, com versões explícitas.
  Linux usa Docker Engine nativo; Windows usa Docker Engine dentro do WSL 2.
  Docker Desktop não é obrigatório.
- Executar o processo ASP.NET Core diretamente, sem Windows Service/systemd dentro da imagem.
  A #59 retira integrações/scripts nativos obsoletos e mantém o desenvolvimento com dotnet run.
- Publicar somente o HTTPS da aplicação na LAN, na porta padrão 5280 configurável.
  O banco fica na rede interna do Compose, com volumes e credenciais próprios.
- Persistir dados do PostgreSQL separadamente de certificados, chaves de Data Protection
  e arquivos da aplicação. Backup tem destino separado e cópia fora da máquina.
- Manter cultura pt-BR e configurar o fuso de negócio explicitamente; auditoria permanece UTC.
- Criar o único usuário pelos comandos user create/user reset-password, com terminal
  interativo e conexão PostgreSQL correta. Não oferecer cadastro de usuário pela web.
- Preservar hash de senha, bloqueio, expiração e SecurityStamp. Chaves de sessão precisam
  sobreviver à recriação do container.
- Manter HTTPS, configurar no certificado os nomes/IPs do host usados pelos dispositivos
  e persistir o certificado. Não inferir o endereço externo pela rede interna do container.
- Validar inicialização automática do WSL/Docker e acesso LAN após reinício do Windows,
  sem terminal aberto. Política de restart do container não basta como prova desse fluxo.
- Usar imagens versionadas e backup anterior a migrations; a entrega exige um ensaio
  de restauração em PostgreSQL vazio. Detalhes em [persistência PostgreSQL](persistence-postgresql.md).

## Comportamento já implementado

O host usa Blazor Interactive Server e Kestrel em todas as interfaces. Na mesma porta,
a conexão TLS segue para HTTPS; HTTP recebe 307 para a URL HTTPS equivalente.
Os nomes/IPs do certificado vêm de `Hosting:Names` (`AGS_HOST_NAMES` no Compose); sem configuração (`dotnet run`),
são detectados pelas interfaces da máquina. As chaves do Data Protection ficam em `DataDirectory`, para a sessão sobreviver
à recriação do container.

Há [login](../identity/login.md), usuário por [comando](../identity/user-creation.md) e
[página de certificado](../web/https-certificate.md). Somente login, seu endpoint de entrada
e arquivos estáticos são anônimos. As demais páginas/endpoints exigem autenticação.

## Consequências e alternativas

Docker acrescenta operação de volumes, rede e credenciais, padronizando instalação e atualização.
O certificado ainda precisa ser confiado nos dispositivos. A aplicação funciona sem internet
após obter as imagens; buscar uma nova versão pode exigir acesso ao registro.

O serviço nativo foi a escolha inicial, superada por Docker e removida. A hipótese anterior de que
Docker no Windows exigiria Docker Desktop foi descartada: o Docker Engine roda dentro do WSL 2. O que ainda precisa de teste
em campo (reinício do Windows, celular na LAN) está em [Limites da validação](../web/docker-deployment.md#limites-da-validação).

Desktop com Avalonia não atendia ao acesso pelo celular. SPA/WebAssembly e microsserviços
acrescentariam peças sem necessidade demonstrada. Acesso pela internet, Kubernetes e
alta disponibilidade continuam fora do escopo aprovado.
