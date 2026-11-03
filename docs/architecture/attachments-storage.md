# Armazenamento de anexos

Origem: [#73](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/73).

## Contexto

Comprovantes de até 20 MB precisam preservar os bytes originais. DataDirectory é compactada por inteiro em cada backup; misturá-los a certificados repetiria todos os documentos diariamente.

## Decisão

Attachments é independente, com DbContext, migrations e schema `attachments`. Só metadados ficam no PostgreSQL. Os arquivos ficam em pasta própria e previamente montada, nunca dentro de DataDirectory ou backup. Transactions e Invoices consomem Contracts e validam o dono, sem FK, JOIN ou dependência inversa.

O upload limita o fluxo, calcula SHA-256 e identifica o conteúdo (assinatura e término para PDF; codec local para JPEG, PNG e WebP), sem confiar na extensão nem modificar o original. Isso identifica o formato; não lê o conteúdo nem faz análise antivírus. SkiaSharp já é usado por Accounts, com versão central e licenças existentes; nenhuma nova biblioteca foi introduzida.

Arquivo temporário e destino ficam no mesmo disco. O movimento atômico precede os metadados; falha no banco compensa removendo o arquivo. O índice único por dono e hash recusa duplicados, inclusive na lixeira. A pasta usa a data UTC do upload e um Guid v7 gerado pelo domínio. Nome original nunca vira caminho. Download é autenticado e usa `nosniff`.

Remoção é lógica por 30 dias. A rotina diária exclui arquivo e registro após o prazo. Um advisory lock do PostgreSQL permite coordenar a limpeza com os comandos em outro processo. Donos inexistentes são recusados pelos módulos na restauração.

## Alternativas

- Bytes no banco: simples e atômico, mas aumenta todos os dumps; adequado apenas aos logos pequenos normalizados.
- DataDirectory: reutilizaria a montagem, mas repetiria todos os arquivos no tar diário.
- S3/MinIO: adicionaria serviço e credenciais sem necessidade nesta instalação local.
- Alterar imagens: descartado porque documentos são evidência e devem conservar seus bytes.

## Consequências

Banco e arquivos exigem recuperação conjunta. Uma interrupção abrupta entre movimento e persistência pode deixar um órfão, que precisa de reconciliação. Falhas normais são compensadas. Não há garantia de transação distribuída com o sistema de arquivos. A aplicação continua disponível quando a pasta de anexos está indisponível.

## Recuperação e exportação (#76)

O manifesto v2 inclui o SHA-256 de `attachments.json`; cada índice registra caminho, tamanho e hash dos documentos ativos e da lixeira. Os bytes ficam numa única árvore `<backup>/attachments/`, copiada incrementalmente. O índice é lido depois do dump. Um advisory lock de sessão impede upload, limpeza, reconciliação destrutiva e outro backup de disputar os arquivos. O upload dos módulos mantém esse bloqueio até a confirmação da operação financeira, evitando que a reconciliação veja como órfão um arquivo cujos metadados ainda não foram confirmados. A conexão do bloqueio suprime a transação ambiente, para não promover a operação para uma transação distribuída.

Na restauração, a conferência e a cópia dos arquivos ausentes antecedem o comando que altera o banco. Um destino existente e adulterado é recusado; não se sobrescreve evidência silenciosamente. Arquivos extras permanecem até reconciliação explícita. O pool do NpgsqlDataSource do processo também é esvaziado antes de exigir exclusividade. A retenção só elimina bytes sem referência em todos os índices mantidos; índice inválido interrompe a limpeza compartilhada de forma conservadora.

Mantemos leitura de v1 e v2. Somente v2 exige o índice. Não duplicamos documentos em cada pacote nem usamos links físicos, que complicariam a cópia entre sistemas de arquivos. Não usamos ZIP como backup reimportável: ele compõe nomes pela referência financeira e serve apenas para consulta/exportação, em fluxo, com índice CSV e checksums. O custo aceito é consultar os metadados em memória; os documentos são copiados com buffers limitados, sem carregar todos os bytes em memória.

No HTTP, o ZIP é preparado em arquivo temporário privado e removido ao encerrar a resposta ou em caso de falha. Isso permite conferir os hashes antes dos cabeçalhos e evita I/O síncrono na resposta durante a finalização das entradas comprimidas. O trade-off é precisar de espaço temporário proporcional ao ZIP; a memória continua limitada aos buffers e metadados.
