# Logos de bancos

Implementado na [#54](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/54).

Em **Contas e cartões → Bancos → Adicionar / editar logo**, envie PNG, JPEG ou WebP de até 5 MB e 4096 × 4096 pixels. O formato é reconhecido e decodificado no servidor, independentemente do nome do arquivo. Arquivos incompletos e formatos diferentes são rejeitados sem substituir o logo atual.

A prévia permite zoom de 1× a 5× e deslocamento horizontal/vertical. O recorte mantém a proporção original e gera PNG RGBA de 256 × 256 com transparência fora do círculo. Salvar usa o mesmo processamento da prévia; cancelar descarta a seleção. Remover exige confirmação. Imagens animadas fornecem somente o primeiro quadro; metadados do original não são conservados.

O original não é persistido. O PNG normalizado e sua versão ficam na linha do banco, sem alterar contas, cartões ou movimentos financeiros. `IBankLogoCatalog` fornece bytes e versão por ExternalId. O endpoint autenticado `/bank-logos/{externalId}` serve a imagem localmente; o componente `BankLogo` usa 40 pixels e referência versionada. Nome e iniciais mantêm a identificação sem imagem. Consumidores futuros de relatórios podem reutilizar o contrato e o PNG com dimensões de exibição próprias, sem outra conversão.

O dump PostgreSQL já inclui os logos. O ensaio `scripts/rehearse-recovery.sh` confere bytes/versão após recriar containers e restaurar uma instalação vazia. Não há download externo, CDN ou arquivo de usuário versionado.

Veja [armazenamento e processamento](../architecture/bank-logo-storage.md).
