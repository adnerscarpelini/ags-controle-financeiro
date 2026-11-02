# Certificado HTTPS

Origem: [#37](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/37). Decisão geral em [web-hosting.md](../architecture/web-hosting.md).

**Estado:** o comportamento abaixo está implementado na execução nativa atual.
Na [#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59), o certificado
passará a usar endereços externos configurados do host e armazenamento persistente do container.
A autodetecção interna descrita abaixo não comprova acesso HTTPS pela LAN no Docker/WSL.

## Comportamento

- **Primeira execução:** a aplicação gera um certificado autoassinado (RSA 2048, SHA-256, validade de 5 anos) em `<DataDirectory>/certs/ags-finance.pfx`, mais a cópia pública `ags-finance.crt`. O nome (CN) e os nomes alternativos (SAN) são o nome do computador, `localhost`, `127.0.0.1`, `::1` e os IPv4 das interfaces ativas.
- **Reaproveitamento:** nas execuções seguintes o mesmo arquivo é usado, então a impressão digital não muda.
- **Regeneração:** o certificado é gerado de novo quando falta algum nome ou IP atual da máquina (por exemplo, o IP mudou), quando faltam menos de 30 dias para vencer ou quando o arquivo está corrompido. Um IP que deixou de existir não força a regeneração, para que adaptadores virtuais que aparecem e somem não troquem o certificado a cada reinício. Depois de regenerado, o `.crt` novo precisa ser instalado de novo nos dispositivos.
- **Certificado próprio:** `Https:CertificatePath` (arquivo `.pfx`) e `Https:CertificatePassword` em `appsettings.json` (ou `Https__CertificatePath` e `Https__CertificatePassword` no ambiente do serviço). Com o caminho preenchido, nada é gerado.
- **Falha:** `.pfx` ausente, senha errada ou arquivo inválido registram um erro crítico no log e o serviço encerra com código 1.
- **Chave privada:** o `.pfx` é gravado sem senha, com modo 600 no Linux. No Windows vale a ACL da pasta de dados, restrita pelo script de instalação.

## HTTP e HTTPS na mesma porta

O endpoint do Kestrel (`Hosting/HttpsEndpoint.cs`) lê o primeiro byte da conexão. Handshake TLS segue para o HTTPS; qualquer outro tráfego recebe `307 Temporary Redirect` para `https://<mesmo host e porta><caminho e query>`. Pedidos malformados (host ou caminho com caracteres fora de uma lista segura, sem linha de requisição) recebem `400`. O redirecionamento é `307`, e não `301`, para o navegador não guardar o redirecionamento em cache.

## Página Configurações

Mostra a impressão digital SHA-256, os nomes cobertos, a validade, o caminho do `.crt` e as instruções para confiar no certificado no Windows, no Linux, no Android e no iPhone. Ao instalar, confira se a impressão digital é a mesma da página.

## Arquivos

| Arquivo | Papel |
| --- | --- |
| `Hosting/LocalCertificate.cs` | Gera, grava, reaproveita e valida o certificado; carrega o `.pfx` configurado. |
| `Hosting/LocalNames.cs` | Descobre nome do computador, `localhost` e IPs das interfaces. |
| `Hosting/ServerCertificate.cs` | Singleton que carrega o certificado uma vez, sob demanda. |
| `Hosting/HttpsEndpoint.cs` | Configura o endpoint HTTPS com o redirecionamento de HTTP simples. |
| `Components/Pages/Settings.razor` | Página de Configurações. |
