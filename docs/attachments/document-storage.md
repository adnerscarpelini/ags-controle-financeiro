# Documentos anexados

Origem: [#73](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/73).

São aceitos PDF, JPEG, PNG e WebP de até 20 MB por arquivo. O conteúdo determina o formato; trocar a extensão não converte um documento. O original é preservado e seu nome é usado apenas para apresentação e download.

Os arquivos são guardados pela data do upload; editar a data financeira não muda seu caminho. Cada documento pertence a um dono identificado publicamente por Guid. O mesmo conteúdo no mesmo dono é recusado, inclusive enquanto está na lixeira.

A lixeira permite download e restauração por 30 dias. Depois a rotina diária elimina arquivo e registro. A restauração exige que o dono ainda exista. Documentos não são importados como dados financeiros e não fazem parte do CSV.

Configuração e preparação da pasta: [Docker Compose](../web/docker-deployment.md#pasta-de-anexos-73). Decisão técnica: [armazenamento](../architecture/attachments-storage.md).
