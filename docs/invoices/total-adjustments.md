# Ajuste do total de fatura

A [#52](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/52)
oferece Ajustar valor em fatura Aberta/Fechada. A prévia distingue total bruto,
saldo restante, total correto e diferença. Cancelar não grava nada. Se o bruto
mudar antes da confirmação, a operação pede uma nova conferência.

Invoices expõe o caso de uso e sua política de estado. Transactions registra um
InvoiceAdjustment imutável pela diferença, sem estabelecimento ou categoria
cadastral fictícios. O contrato identifica IsInvoiceAdjustment e a classificação
própria Ajuste de fatura; identificadores vazios de descrição/categoria representam
essa classificação interna, não uma referência a cadastro inexistente.

A data é a confirmação no fuso do negócio; o mês contábil continua o da fatura.
Aumentar o total aumenta despesas; reduzi-lo reduz despesas, sem receita comum.
Total bruto negativo é aceito. Diferença zero não gera lançamento; correções são
novos ajustes que conservam os anteriores. A confirmação é idempotente.

Ajustar não reabre, não edita compras, não altera pagamentos/débitos anteriores e
não quita a fatura automaticamente, inclusive quando resulta zero/crédito.
Paga bloqueia o comando pelo guard do módulo, também em chamadas diretas.
Os comandos genéricos de edição/exclusão não alcançam ajustes.

Em fatura Aberta, o total correto é comparado só com o **lançado até hoje**, sem as recorrentes previstas
(ver [recorrentes previstas](recurring-forecast.md)); em Fechada, com o total completo.

Composição, lançamentos e totais de despesas incluem a diferença uma única vez.
A grade mantém filtros e paginação estável: consulta a janela de compras necessária
e combina os ajustes correspondentes, sem carregar todas as compras para paginar.
Testes cobrem páginas sem lacunas/duplicatas e consulta com 10 mil lançamentos.
