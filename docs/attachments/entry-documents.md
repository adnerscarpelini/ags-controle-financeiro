# Documentos de lançamentos

Origem: [#74](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/74).

Escolha Conta/Boleto, Comprovante ou Outro ao criar ou editar um lançamento ou registrar a baixa. Pode selecionar vários arquivos. O clipe no histórico mostra a quantidade de documentos ativos e abre as ações de envio, abertura, download e remoção.

Compra parcelada compartilha os documentos da compra em todas as parcelas, inclusive após antecipações. Uma recorrência guarda cada anexo na ocorrência escolhida. Edições de qualquer alcance preservam o ExternalId das ocorrências mantidas, mesmo quando a série é dividida. Duplicar um lançamento não copia anexos.

Compras em faturas Fechadas ou Pagas aceitam anexos e remoção, sem alterar valores nem situação. A exclusão do lançamento envia os documentos à lixeira; na compra parcelada, isso só ocorre ao excluir a última parcela.

Salvar e enviar são etapas distintas. Se o documento falhar, o lançamento ou a baixa permanece. Use **Reenviar anexos** para tentar de novo; a tela conserva somente os arquivos cujo envio falhou. Novo/limpar abandona essa seleção e inicia outro lançamento. Se o navegador perdeu acesso ao arquivo, selecione-o novamente. Documentos não são exportados no CSV.

Na arquitetura, `IEntryAttachmentCatalog` valida o dono e usa o mesmo limite de operação financeira das exclusões, sem consultar tabelas de Attachments. A compensação do upload continua sendo responsabilidade de Attachments; o seletor não contém regras financeiras.
