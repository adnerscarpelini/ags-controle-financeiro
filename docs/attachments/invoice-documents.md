# Documentos de faturas e pagamentos

Origem: [#75](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/75).

**Anexos** está disponível nas ações da fatura em qualquer situação: Aberta, Fechada ou Paga. Tipos: Fatura, Comprovante ou Outro. Anexar e remover não modifica situação, compras, totais nem pagamentos. O primeiro envio cria a fatura como Aberta quando ela ainda não tem registro; apenas consultar o seletor não cria registros. Reabrir mantém os documentos.

O diálogo de pagamento oferece seleção opcional de vários documentos, vinculados ao pagamento confirmado e acessíveis pelo histórico. Confirmar o pagamento é uma etapa definitiva, anterior ao envio. A tela atualiza a lista imediatamente após a confirmação e, se o envio falhar, oferece **Reenviar somente anexos**. Essa ação usa o identificador do pagamento já gravado e nunca chama novamente a confirmação financeira. **Fechar** permite sair e reenviar depois pelo histórico.

A identidade estável da confirmação e a operação financeira existente continuam impedindo pagamentos duplicados. Attachments recusa conteúdo duplicado no mesmo dono. A falha de arquivo não desfaz nem reabre um pagamento. A remoção segue a lixeira de 30 dias e a restauração exige o dono existente.

`IInvoiceAttachmentCatalog` valida o dono pelos dados de Invoices e compõe os Contracts de Attachments; não há FK ou JOIN com outro módulo. Upload e remoção não chamam o ciclo de fechamento. Os seletores e ações usam os controles responsivos e os tokens locais de toque, sem recursos de CDN.
