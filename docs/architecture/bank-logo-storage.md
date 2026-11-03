# Armazenamento e processamento de logos

Decisão implementada na [#54](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/54).

Guardamos apenas o PNG normalizado (256 × 256) em `accounts.banks.LogoData`, com `LogoVersion` Guid v7. São imagens pequenas e opcionais: manter bytes e cadastro na mesma transação evita arquivos órfãos e torna backup/restauração consistentes pelo dump existente. Arquivos em volume exigiriam coordenação adicional com o banco; armazenamento na camada gravável do container perderia dados ao recriá-lo.

`ILogoProcessor` separa a aplicação do decodificador. Somente Infrastructure usa SkiaSharp e os ativos nativos Linux sem dependência de fontes. O domínio valida o tamanho final; a aplicação usa a operação financeira compartilhada ao persistir. O processamento limita entrada a 5 MB, dimensões a 4096 por lado e zoom/posição a intervalos finitos antes de alocar pixels. A conversão em sRGB e a codificação PNG descartam metadados do arquivo original. Há custo de memória do bitmap decodificado, limitado a aproximadamente 64 MiB por imagem, além dos buffers.

Escolhemos [SkiaSharp 4.154.0](https://www.nuget.org/packages/SkiaSharp/), com [ativos Linux](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/), para PNG/JPEG/WebP, escala e transparência no Windows e Linux. Evitamos System.Drawing por sua restrição de plataforma e uma solução no navegador que confiaria no arquivo final enviado. A dependência nativa aumenta a imagem Docker, em troca de um único processamento para prévia e gravação. Licença MIT e avisos de terceiros estão preservados em `licenses/SkiaSharp` e copiados para a distribuição; [licença original](https://github.com/mono/SkiaSharp/blob/main/LICENSE.txt).

`IBankLogoCatalog` expõe somente DTOs, bytes e ExternalId. O host transforma isso em resposta PNG autenticada, com ETag; a URL versionada é uma referência local, sem acesso ao Id interno. O contrato não depende de ASP.NET nem expõe entidades EF.
