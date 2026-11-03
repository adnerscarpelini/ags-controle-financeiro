# Layout CSV oficial e exportação de cadastros

A [#61](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/61) define o
layout CSV oficial do sistema e exporta os cadastros nele. É a única troca de arquivos
suportada para cadastros e lançamentos. Não há leitura de PDF, OFX, XLS/XLSX, OCR, layouts
de bancos nem migração da antiga planilha. **CSV não substitui o backup**: a recuperação
completa é o backup PostgreSQL ([#31](../web/docker-deployment.md)).

## Regras do formato (todas as versões)

| Item | Regra |
| --- | --- |
| Codificação | UTF-8; a exportação escreve com BOM (para planilhas acertarem os acentos) e a leitura aceita com ou sem BOM. Outro conteúdo é rejeitado. |
| Separador | Vírgula. Fim de linha CRLF na escrita; CRLF ou LF na leitura. |
| Aspas | Campo com vírgula, aspas ou quebra de linha vai entre aspas duplas; aspas internas são duplicadas (RFC 4180). Aspas fora do lugar rejeitam o arquivo. |
| Linha 1 | `ags-finance-csv,<tipo>,<versão>`: marcador, tipo do arquivo e versão do layout. |
| Linha 2 | Cabeçalho exatamente igual ao do tipo (nomes e ordem). Ausente, extra, renomeado ou fora de ordem: arquivo rejeitado. |
| Demais linhas | Um registro por linha, com o número exato de colunas. Linhas em branco rejeitam o arquivo. |
| Datas | `yyyy-MM-dd`. |
| Valores monetários | Inteiro em centavos (`123456` = R$ 1.234,56), sem separador nem símbolo. Nunca decimal. |
| Booleanos | `true` ou `false`. |
| Enums | O nome em inglês listado em cada tipo, com a mesma grafia. |
| Opcional | Campo vazio, sem aspas. |
| Identidades | `external_id` e referências `*_external_id` são o `ExternalId` público (GUID formato `D`). O `Id` interno nunca sai. |
| Limites | Até 10 MB e 50.000 registros por arquivo. |
| Versão | Cada tipo tem a sua (hoje todos `1`). Versão desconhecida é rejeitada; não se infere formato. |

**Texto seguro para planilhas.** Em campos de texto (nomes, rótulos, detalhes), se o valor começa
com zero ou mais apóstrofos seguidos de `=`, `+`, `-`, `@`, TAB ou CR, a exportação acrescenta um
apóstrofo no início. A leitura remove exatamente um. A regra é simétrica: `=SOMA(A1)` sai como
`'=SOMA(A1)` e volta como `=SOMA(A1)`; `'=x` sai como `''=x` e volta `'=x`. Texto comum com
apóstrofo (`'abc`) não é alterado. Identificadores, números e datas não passam por esta regra.

## Tipos de cadastro (versão 1)

Cada arquivo é gerado/lido por tipo, com nome `cadastro-<tipo>-v1.csv`. A ordem de importação
(#28) respeita as referências: quem referencia vem depois de quem é referenciado.

| Ordem | Tipo (linha 1) | Colunas |
| ---: | --- | --- |
| 1 | `banks` | `external_id`, `name` |
| 2 | `accounts` | `external_id`, `bank_external_id`, `name`, `kind` (`Checking`, `Investment`, `Savings`), `color`, `is_active`, `opening_balance_cents`, `opening_date` |
| 3 | `cards` | `external_id`, `account_external_id`, `name`, `brand` (`Visa`, `Mastercard`, `Elo`), `credit_limit_cents`, `closing_day`, `due_day`, `includes_closing_day`, `color`, `is_active` |
| 4 | `card_digits` | `external_id`, `card_external_id`, `digits`, `usage` (`Own`, `Shared`, `Subscription`), `label`, `is_active` |
| 5 | `categories` | `external_id`, `name`, `kind` (`Expense`, `Income`, `Transfer`), `is_active` |
| 6 | `subcategories` | `external_id`, `category_external_id`, `name`, `is_active` |
| 7 | `merchants` | `external_id`, `name`, `default_subcategory_external_id` (opcional), `is_active` |
| 8 | `people` | `external_id`, `name`, `payer_description` |

Cadastros arquivados/desativados saem com `is_active=false` ([#53](../accounts/archiving.md)) e
mantêm o estado. Logos de banco (binários), regras de rateio, filtros salvos, senhas, hashes e
credenciais não fazem parte do CSV.

## Lançamentos e séries recorrentes (versão 1, [#62](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/62))

Dois arquivos, `lancamentos-transactions-v1.csv` e `lancamentos-recurring-series-v1.csv`, com as regras do formato acima.

**Cobertura.** Receitas e despesas: avulsas, de cartão, parcelas e ocorrências recorrentes, com o estado
Pendente/Efetivado. **Ficam de fora**, por decisão desta versão: ajustes de fatura (#52), transferências
entre contas e pagamentos/cashbacks/descontos de fatura. Eles nunca são tratados como despesa, os pagamentos
continuam imutáveis, e a importação (#29) rejeita esses tipos em vez de interpretá-los.

| Tipo (linha 1) | Colunas |
| --- | --- |
| `transactions` | `external_id`, `date`, `accounting_month` (`yyyy-MM`, o mês gravado, inclusive o escolhido manualmente), `kind` (`Income`, `Expense`), `amount_cents` (positivo; o sinal vem de `kind`), `merchant_external_id`, `subcategory_external_id`, `account_external_id`, `card_external_id` (opcional), `card_digits_external_id` (opcional), `details`, `is_effective`, `effective_date` (opcional), `installment_purchase_external_id`, `installment_number`, `installment_count` (os três opcionais, juntos), `recurring_entry_external_id` (opcional), `occurrence_month` (`yyyy-MM-dd`, opcional) |
| `recurring_series` | `external_id`, `start_date`, `day`, `end_month` (`yyyy-MM`, opcional), `merchant_external_id`, `subcategory_external_id`, `account_external_id`, `card_external_id` (opcional), `card_digits_external_id` (opcional), `amount_cents`, `details`, `accounting_month` (opcional), `is_active` |

**Vínculos explícitos.** Parcelas são agrupadas pelo `installment_purchase_external_id` e numeradas por
`installment_number`/`installment_count`; ocorrências recorrentes apontam para a série por
`recurring_entry_external_id` e identificam o mês por `occurrence_month`. Nada é inferido por
descrição, `(x/y)` ou semelhança de valor. A definição da série vai no arquivo `recurring_series`, que
contém as séries referenciadas pelos lançamentos do mesmo filtro. Os meses excluídos de uma série
("somente esta" apagada) não existem no Contract e portanto não são exportados.

**Filtros.** Os mesmos da tela Lançamentos ([#15](../transactions/entry-filters.md)): mês contábil, intervalo de
datas, conta, cartão, final, categoria, subcategoria, texto, situação e tipo, ou um filtro salvo
([#33](../transactions/saved-filters.md)) aplicado ao mês/intervalo. Valor de filtro inválido rejeita o pedido.
Endpoints: `GET /csv/lancamentos`, `/csv/lancamentos/series` (mesma query string), e `/modelo` e `/exemplo` de
cada um. Sem registros, o arquivo traz só versão e cabeçalhos.

**Somente consulta.** A exportação usa a mesma consulta da tela Lançamentos, que materializa as ocorrências
recorrentes do período; nada é efetivado, nenhum pagamento é criado e saldos e faturas não mudam.

## Modelo, exemplo e exportação

A tela **Arquivos CSV** (`/arquivos-csv`, acessível por Cadastros) baixa, por tipo, a exportação
atual, o **modelo vazio** (só versão e cabeçalhos) e um **exemplo** com dados somente fictícios.
Os endpoints são `GET /csv/cadastros/{tipo}`, `/modelo` e `/exemplo` e exigem login. A exportação é
somente leitura: não altera cadastros nem saldos.

## Estado

Implementados: layout, escrita e leitura estrita, exportação de cadastros (#61) e de lançamentos e séries (#62), modelos e exemplos.
A importação é a #28 (cadastros) e a #29 (lançamentos).
