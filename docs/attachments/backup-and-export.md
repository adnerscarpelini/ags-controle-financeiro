# Backup, reconciliação e exportação

Origem: [#76](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/76).

## Recuperação

Cada pacote v2 inclui `attachments.json`, protegido pelo hash do manifesto, com caminho imutável, tamanho e SHA-256 de todos os documentos, inclusive da lixeira. Os bytes estão numa única árvore `<backup>/attachments/`. Backups sucessivos copiam somente arquivos que faltam; cópias existentes são conferidas. A retenção só apaga um arquivo quando nenhum pacote mantido o referencia. Um índice inválido impede essa limpeza compartilhada.

Copie a **pasta de backup inteira**, com todos os pacotes e `attachments/`, para fora da máquina. A subpasta de um pacote isolado não basta. Na instalação nova, prepare a pasta própria de anexos com UID 1654 e restaure conforme o [guia do Compose](../web/docker-deployment.md). Todos os arquivos necessários são conferidos e copiados antes de executar a restauração do banco. Falha na cópia preserva o estado financeiro atual. Arquivos já existentes com conteúdo divergente são recusados e arquivos extras não são apagados automaticamente.

Backups v1 continuam aceitos sem `attachments.json`. As migrations criam o schema novo após a restauração de versões anteriores. ZIP não é um formato de recuperação.

## Reconciliação

```bash
docker compose run --rm app attachments verify
docker compose run --rm app attachments verify --clean-orphans
```

A primeira forma lista ausentes, corrompidos e arquivos sem registro, sem apagar nada. A segunda remove **apenas os arquivos sem registro**; não apaga registros nem tenta consertar arquivos corrompidos. Há código de saída de falha enquanto faltarem arquivos ou houver corrupção ou órfãos não removidos. Temporários internos de upload não são tratados como documentos definitivos. Execute após conferir a pasta configurada e manter uma cópia fora da máquina.

A limpeza da lixeira, o upload, o backup e a reconciliação destrutiva compartilham um bloqueio no PostgreSQL. Upload e download também são recusados durante a manutenção, inclusive quando uma tela já estava aberta.

## Página e lixeira

Em **Anexos** (`/anexos`), filtre por tipo e período de upload, confira o uso de disco e alterne para a lixeira. Abrir e baixar continuam disponíveis dentro dos 30 dias. Restaurar valida se o lançamento, a compra, a fatura ou o pagamento ainda existe. Dono excluído impede a restauração, com mensagem; o documento ainda pode ser baixado durante o prazo.

## ZIP

Na página, informe o período financeiro e o tipo ou use:

```bash
docker compose run --rm app attachments export --from 2027-01-01 --to 2027-01-31 --kind Receipt --output /backups/comprovantes.zip
```

Omitir `--kind` inclui todos os tipos. Valores: `Bill`, `Receipt`, `Invoice`, `Other`. O comando recusa sobrescrever um arquivo existente. Exporte fora da pasta ativa de anexos. Se não houver documentos, a tela e o comando exibem mensagem, sem gerar ZIP vazio. Itens na lixeira ficam de fora.

- Lançamentos: data do lançamento, com limites inclusivos. Na compra parcelada, usa a primeira parcela ainda existente, pela ordem das parcelas, mesmo se outra parcela tiver data menor. Uma única cópia do documento entra no ZIP.
- Faturas e comprovantes de pagamentos: mês da fatura, inclusive os meses dos dois limites informados. A data do pagamento e a data do upload não decidem esse filtro.
- Pastas: `lancamentos/2027/01/2027-01-03_comprovante_descricao_id8.pdf` e `faturas/cartao/2027-01/2027-01_fatura_id8.pdf`. Nomes são normalizados e colisões usam o identificador completo.
- `indice.csv`: arquivo, tipo, dono (ExternalId), data de referência, nome original, tamanho e SHA-256, em UTF-8 com separador `;`. Campos textuais são escapados e neutralizam fórmulas de planilha.
- `SHA256SUMS`: confere todos os documentos e o índice; o próprio arquivo de somas fica de fora por definição. Após extrair, execute `sha256sum -c SHA256SUMS`.

Os documentos são lidos e compactados em fluxo. O ZIP não é criptografado, não é reimportado e não substitui o backup. A troca real de máquina e o acesso no celular pela LAN exigem validação em campo, além dos testes locais e do ensaio em containers.

O download HTTP prepara o ZIP em um arquivo temporário privado, valida os hashes e o transmite em fluxo. O arquivo temporário é removido ao fechar a resposta ou em caso de falha. Reserve espaço temporário para o ZIP; os documentos não são carregados integralmente na memória.
