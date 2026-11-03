# Armazenamento de anexos

Origem: [#73](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/73).

## Contexto

Comprovantes de até 20 MB precisam preservar os bytes originais. DataDirectory é compactada por inteiro em cada backup; misturá-los a certificados repetiria todos os documentos diariamente.

## Decisão

Attachments é independente, com DbContext, migrations e schema `attachments`. Só metadados ficam no PostgreSQL. Os arquivos ficam em pasta própria e previamente montada, nunca dentro de DataDirectory ou backup. Transactions e Invoices consomem Contracts e validam o dono, sem FK, JOIN ou dependência inversa.

O upload limita o fluxo, calcula SHA-256 e identifica o conteúdo (assinatura e término para PDF; codec local para JPEG, PNG e WebP), sem confiar na extensão nem modificar o original. Isso identifica o formato; não lê o conteúdo nem faz análise antivírus. SkiaSharp já é usado por Accounts, com versão central e licenças existentes; nenhuma nova biblioteca foi introduzida.

Arquivo temporário e destino ficam no mesmo disco. O movimento atômico precede os metadados; falha no banco compensa removendo o arquivo. O índice único por dono e hash recusa duplicados, inclusive na lixeira. A pasta usa a data UTC do upload e um Guid v7 gerado pelo domínio. Nome original nunca vira caminho. Download é autenticado e usa `nosniff`.

Remoção é lógica por 30 dias. A rotina diária exclui arquivo e registro após o prazo. Um advisory lock do PostgreSQL permite coordenar a limpeza com os comandos em outro processo. Donos inexistentes serão recusados pelos módulos na restauração.

## Alternativas

- Bytes no banco: simples e atômico, mas aumenta todos os dumps; adequado apenas aos logos pequenos normalizados.
- DataDirectory: reutilizaria a montagem, mas repetiria todos os arquivos no tar diário.
- S3/MinIO: adicionaria serviço e credenciais sem necessidade nesta instalação local.
- Alterar imagens: descartado porque documentos são evidência e devem conservar seus bytes.

## Consequências

Banco e arquivos exigem recuperação conjunta. Uma interrupção abrupta entre movimento e persistência pode deixar um órfão, que precisa de reconciliação. Falhas normais são compensadas. Não há garantia de transação distribuída com o sistema de arquivos. A aplicação continua disponível quando a pasta de anexos está indisponível.
