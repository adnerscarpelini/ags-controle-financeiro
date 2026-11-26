# Cashback dos cartões

Origem: [#153](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/153). O ciclo atravessa três módulos,
sem dependência nova entre eles: **Accounts** guarda o percentual do cartão, **Transactions** grava o percentual em cada
compra e **Invoices** soma o previsto, mostra o painel e registra o recebimento.

## Cadastro do cartão (Accounts)

A janela do cartão tem a chave **Usa cashback**. Ligada, pede o **percentual**: maior que 0 e até 100, com até duas casas
decimais (`CreditCard.SetCashback`); fora disso o cadastro é recusado com mensagem. Desligada, nenhum percentual é gravado
(`CashbackPercent` nulo). `CreditCardDetails` expõe `CashbackPercent` e `UsesCashback`, e a carteira mostra
"Cashback 1,5%" no cartão configurado.

`SaveCreditCardRequest` recebe o percentual em `CardCashback`; `CardCashback.None` desliga. Quando o pedido não informa cashback
(nulo), a configuração atual é mantida, como na importação CSV de cadastros, cujo layout de `cards` não tem essa coluna (cartões
criados pela importação chegam sem cashback). Arquivar e desarquivar preservam a configuração.

## Percentual gravado na compra (Transactions)

- Toda despesa de cartão grava em `TransactionEntry.CashbackPercent` o percentual vigente do cartão **no ato do lançamento**
  (0 quando o cartão não usa cashback). Mudar o cadastro do cartão vale só para compras novas; o histórico não é recalculado.
- Editar a compra sem trocar de cartão preserva o percentual gravado, mesmo que o cartão tenha mudado; editar o valor recalcula
  o previsto com esse percentual. Trocar de cartão grava o percentual vigente do novo cartão.
- Parcelas: cada parcela grava o percentual e tem o próprio previsto, na sua fatura. A diferença de centavos das parcelas segue a
  regra de [parcelas](../transactions/installments.md); o previsto é calculado sobre o valor de cada parcela.
- Ocorrências recorrentes de cartão gravam o percentual que o cartão tiver quando a ocorrência é gerada.
- A importação CSV de lançamentos passa pelas mesmas validações e grava o percentual vigente do cartão; o
  [layout CSV](../imports/csv-layout.md) não mudou.
- Receitas no cartão gravam 0%: só despesas de cartão têm cashback.

### Regra de arredondamento

Previsto da compra = **valor absoluto × percentual / 100**, arredondado em centavos pela regra de
[`Money`](../shared-kernel/money-and-reference-month.md) (2 casas, `MidpointRounding.AwayFromZero`), em
`CashbackCalculation.Expected`. Exemplo fictício: R$ 33,33 a 1,5% dá 0,49995, que vira **R$ 0,50**; R$ 150,00 a 1% dá R$ 1,50.

O previsto é derivado na leitura (`ExpectedCashback`), não é uma coluna: só o percentual fica gravado.

## Previsto da fatura e detalhe

O previsto da fatura (`InvoiceDetails.ExpectedCashback`) é a soma do previsto das compras, sem os
[ajustes de fatura](total-adjustments.md), que não geram cashback. O resultado nunca fica negativo.

O detalhe da fatura mostra a coluna **Cashback** (valor e percentual, ou "—" sem percentual) e o total **Cashback previsto** no
resumo quando o cartão usa cashback ou a fatura já tem previsto maior que zero. Nos demais cartões, coluna e total não aparecem.
O menu da fatura e o menu de mais ações da tela Cartões oferecem **Cashback**, que abre o painel, e **Descontos**
(`/cartoes?abrir=descontos` abre essa janela).

## Painel `/cashback`

Item **Cashback** no menu lateral, logo depois de Cartões. Links antigos de `/cartoes?abrir=cashback` levam ao painel.

- O seletor de mês usa o **mês da fatura** (mês de referência, que é o do vencimento): em dezembro aparece a fatura que vence em
  dezembro, com as compras de novembro.
- Uma linha por cartão com previsto no mês, ou com recebimento já confirmado: cartão e fatura, previsto, recebido e situação.
  Cartão sem cashback ou fatura sem previsto não gera linha.
- Situações, derivadas na leitura (só o recebimento é gravado):

  | Situação | Quando | Ação |
  | --- | --- | --- |
  | Previsto | Fatura Aberta ou Fechada | Nenhuma, com a explicação de que é preciso a fatura estar Paga |
  | A receber | Fatura Paga sem recebimento, destacada com `Warning` | Receber |
  | Recebido | Recebimento confirmado, com previsto e recebido lado a lado | Desfazer |

  Com recebimento, o previsto exibido é o gravado na confirmação. A diferença entre previsto e recebido fica visível e não vira
  pendência.
- Totais do mês: Previsto, A receber e Recebido. Os valores respeitam [Ocultar valores](../web/hide-values.md).
- Histórico do ano, pela **data do recebimento**: data, cartão e fatura, destino e valor, com o total anual. Inclui o cashback
  lançado manualmente antes desta entrega, como abatimento na própria fatura. Recebimentos de R$ 0,00 ficam de fora.

## Receber o cashback

**Receber** só existe com a fatura Paga e abre `CashbackReceiptDialog`: data, valor recebido (padrão o previsto, editável, maior
ou igual a zero) e o destino.

- **Conta corrente:** conta corrente ativa, com a vinculada ao cartão pré-selecionada. Cria uma receita avulsa Efetivada na conta
  e na data informadas (mês de referência da data), com a descrição "Cashback &lt;nome do cartão&gt;", detalhes
  "Cashback da fatura MM/aaaa" e a subcategoria **Cashback › Cashback recebido**. Categoria, subcategoria e descrição são criadas
  na primeira vez; descrição desativada é recusada com o pedido de reativá-la. A receita entra nas receitas e no planejamento
  como qualquer receita recebida, mas não pode ser editada nem excluída em Lançamentos (`IInvoiceWriteGuard.RequireEntryChangeAsync`).
- **Abatimento na fatura Aberta:** uma fatura Aberta do mesmo cartão (as opções vêm de `GetTargetInvoicesAsync`). Cria um
  `InvoicePayment` do tipo Cashback na fatura de destino, ligado ao recebimento por `CashbackReceiptExternalId`, com os detalhes
  "Cashback da fatura MM/aaaa", sem receita. Ele reduz o saldo restante do destino e aparece no histórico como
  "Cashback recebido". Sem fatura Aberta, o destino fica indisponível com a explicação; destino Fechado ou Pago é recusado.
- **R$ 0,00** encerra o cashback como Recebido, sem receita, sem abatimento e sem exigir conta.

O recebimento fica em `invoices.cashback_receipts`, um por fatura, com o previsto no momento da confirmação e o valor recebido.
Recebimento, receita e abatimento rodam numa única [operação financeira](../architecture/financial-operations.md): falha em
qualquer parte não deixa recebimento gravado. A identidade da confirmação torna o envio idempotente; um clique repetido devolve o
mesmo recebimento, sem nova receita ou abatimento.

### Desfazer

Desfazer pede confirmação, exclui a receita (anexos vão para a lixeira) ou remove o abatimento, e a situação volta a A receber.
Desfazer um abatimento é bloqueado quando a fatura de destino está Paga: é preciso reabri-la antes.

Reabrir a fatura de origem ([#93](payments.md#reabertura-com-desfazimento-93)) preserva o recebimento.

## Descontos e cashback manual antigo

A antiga janela "Cashback e descontos" virou **Descontos** (`DiscountsDialog`): cria, edita e exclui somente Desconto, com as
regras da [#19](payments.md). `SaveAllowance` e `DeleteAllowance` recusam o tipo Cashback, inclusive o abatimento gerado por um
recebimento.

Cashbacks lançados à mão antes desta entrega não foram alterados nem migrados: continuam abatendo suas faturas, aparecem somente
leitura como "Cashback lançado manualmente" e entram no histórico anual do painel.

## Migrations e dados existentes

As migrations `CreditCardCashback` (Accounts), `EntryCashback` (Transactions) e `CashbackReceipts` (Invoices) criam as colunas
e a tabela. Cartões existentes ficam sem cashback e compras existentes ficam com 0%; parametrizar dados já gravados é uma tarefa
manual, fora do repositório.

## Fora do escopo

Percentual por categoria, teto de cashback por fatura, edição do percentual na compra, card no Dashboard e relatório anual
de cashback.

## Testes

- `InvoiceTests.Cashback` (Invoices): percentual na compra, edição, troca de cartão, parcelas, arredondamento, ajuste, painel,
  recebimento em conta e por abatimento, R$ 0,00, idempotência, desfazer e reabertura.
- `InvoiceTests.Allowances`, `InvoiceTests.CardSummary`, `InvoiceTests.Payments` e `InvoiceTests.ReopeningPayments`: Descontos sem
  Cashback e cashback manual antigo.
- `CatalogTests` (Accounts): validação e preservação do percentual do cartão.
- `CashbackPageTests`, `CardsPageTests`, `LayoutPagesTests` e `MainLayoutTests` (Web): painel, menus e redirecionamento.
- `MigrationTests` dos três módulos e `DatabaseStartupTests`: colunas e tabela novas.
