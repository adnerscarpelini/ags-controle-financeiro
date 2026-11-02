# Hospedagem web, distribuição e segurança

## Contexto e estado

A aplicação é um servidor web pessoal, para computador e celular na rede local.
O host ASP.NET Core/Blazor, HTTPS e autenticação já estão implementados
([#34](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/34),
[#35](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/35),
[#36](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/36),
[#37](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/37)).
A PWA ainda está pendente na [#38](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/38).

**Destino aprovado, ainda não implementado:** Docker Compose com aplicação e PostgreSQL,
na ordem [#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60) →
[#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59).
A [instalação nativa](../web/service-installation.md) documenta o código atual e será substituída.
O banco já é o [PostgreSQL](persistence-postgresql.md) (#60). Não usar a entrega histórica como decisão
para perpetuar duas distribuições.

## Decisão aprovada

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
Certificados locais atuais usam os nomes/IPs detectados da máquina; a configuração de
endereços externos para Docker será adaptada na #59.

Há [login](../identity/login.md), usuário por [comando](../identity/user-creation.md) e
[página de certificado](../web/https-certificate.md). Somente login, seu endpoint de entrada
e arquivos estáticos são anônimos. As demais páginas/endpoints exigem autenticação.

## Consequências e alternativas

Docker acrescenta operação de volumes, rede e credenciais, padronizando instalação e atualização.
O certificado ainda precisa ser confiado nos dispositivos. A aplicação funciona sem internet
após obter as imagens; buscar uma nova versão pode exigir acesso ao registro.

O serviço nativo foi a escolha inicial, superada por Docker. A hipótese anterior de que
Docker no Windows exigiria Docker Desktop foi descartada. A #59 deve comprovar o caminho WSL
em ambiente real, incluindo firewall e inicialização.

Desktop com Avalonia não atendia ao acesso pelo celular. SPA/WebAssembly e microsserviços
acrescentariam peças sem necessidade demonstrada. Acesso pela internet, Kubernetes e
alta disponibilidade continuam fora do escopo aprovado.
