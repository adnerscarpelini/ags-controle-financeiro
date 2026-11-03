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
| Percentuais | Ponto decimal, de 0 a 100, com até duas casas (`33.33`, `50`). |
| Versão | Cada tipo tem a sua, indicada na linha 1 e no nome do arquivo. Versão desconhecida é rejeitada; não se infere formato. |

**Texto seguro para planilhas.** Em campos de texto (nomes, rótulos, detalhes), se o valor começa
com zero ou mais apóstrofos seguidos de `=`, `+`, `-`, `@`, TAB ou CR, a exportação acrescenta um
apóstrofo no início. A leitura remove exatamente um. A regra é simétrica: `=SOMA(A1)` sai como
`'=SOMA(A1)` e volta como `=SOMA(A1)`; `'=x` sai como `''=x` e volta `'=x`. Texto comum com
apóstrofo (`'abc`) não é alterado. Identificadores, números e datas não passam por esta regra.

## Tipos de cadastro

Cada arquivo é gerado/lido por tipo, com nome `cadastro-<tipo>-v<versão>.csv`. Todos estão na versão 1,
exceto `people`, na versão 2 ([#78](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/78)). A ordem de importação
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
| 8 | `people` | `external_id`, `name`, `default_percent` |

Cadastros arquivados/desativados saem com `is_active=false` ([#53](../accounts/archiving.md)) e
mantêm o estado. Logos de banco (binários), filtros salvos, senhas, hashes e
credenciais não fazem parte do CSV.

## Lançamentos e séries recorrentes (versão 2, [#62](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/62), [#84](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/84))

Dois arquivos, `lancamentos-transactions-v2.csv` e `lancamentos-recurring-series-v2.csv`, com as regras do formato acima. A versão 1 deixou de ser aceita.

**Cobertura.** Receitas e despesas: avulsas, de cartão, parcelas e ocorrências recorrentes, com o estado
Pendente/Pago ou Recebido. **Ficam de fora**, por decisão desta versão: ajustes de fatura (#52), transferências
entre contas e pagamentos/cashbacks/descontos de fatura. Eles nunca são tratados como despesa, os pagamentos
continuam imutáveis, e a importação (#29) rejeita esses tipos em vez de interpretá-los.

| Tipo (linha 1) | Colunas |
| --- | --- |
| `transactions` | `external_id`, `date`, `accounting_month` (`yyyy-MM`, o mês gravado, inclusive o escolhido manualmente), `kind` (`Income`, `Expense`), `amount_cents` (positivo; o sinal vem de `kind`), `merchant_external_id`, `subcategory_external_id`, `account_external_id`, `card_external_id` (opcional), `card_digits_external_id` (opcional), `details`, `is_effective`, `effective_date` (opcional), `installment_purchase_external_id`, `installment_number`, `installment_count` (os três opcionais, juntos), `recurring_entry_external_id` (opcional), `occurrence_month` (`yyyy-MM-dd`, opcional), `reimbursement_person_external_id`, `reimbursement_percent` (os dois opcionais, juntos) |
| `recurring_series` | `external_id`, `start_date`, `day`, `end_month` (`yyyy-MM`, opcional), `merchant_external_id`, `subcategory_external_id`, `account_external_id`, `card_external_id` (opcional), `card_digits_external_id` (opcional), `amount_cents`, `details`, `accounting_month` (opcional), `is_active`, `reimbursement_person_external_id`, `reimbursement_percent` (os dois opcionais, juntos) |

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

## Importação de cadastros ([#28](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/28))

`IRegistryImport` (`Imports.Contracts`) e o painel **Importar cadastros** de `/arquivos-csv` aceitam apenas o
layout acima: versão, tipo e cabeçalho exatos, UTF-8, vírgula, tipos e obrigatoriedade por coluna. PDF, OFX, XLSX,
CSV de banco ou qualquer outro layout são rejeitados; nada é adaptado, aliasado ou inferido.

**Fluxo.** Enviar → prévia (novos, idênticos e problemas com linha e coluna) → confirmar ou cancelar. A prévia não grava
nada; cancelar descarta o conteúdo. O arquivo fica só em memória durante o processamento e não é armazenado.

**Ordem.** Bancos → contas → cartões → finais; categorias → subcategorias → descrições; pessoas. Referências são
resolvidas pelo `ExternalId` já existente no destino; nada é criado para "consertar" um arquivo.

**Atomicidade e invariantes.** Toda a gravação acontece pelos Contracts dos módulos dentro de uma única operação
financeira (`IFinancialOperation`, mesma transação e travas das demais operações). A prévia executa exatamente o mesmo
caminho e descarta a transação, então as invariantes reais dos módulos (nomes únicos, conta corrente para cartão, etc.)
valem já na validação, e qualquer erro em qualquer linha desfaz o arquivo inteiro. Para isso os Contracts de criação
ganharam um `ExternalId` opcional (nulo = gera como antes; usado só ao criar, nunca ao editar).

**Política de conflito.** Mesmo `ExternalId` com os mesmos dados: idêntico, ignorado (reimportar não duplica nem altera).
Mesmo `ExternalId` com dados diferentes: conflito, o arquivo é rejeitado e nada é sobrescrito. Nome já usado por outro
registro: erro do módulo, com linha e coluna.

**Arquivados.** Registro novo com `is_active=false` é criado e arquivado. Registro existente nunca é reativado nem arquivado
pela importação (a diferença de estado é conflito). Contas e cartões arquivados são reativados apenas durante o passo
que cria um cartão ou final dependente e arquivados de novo na mesma operação.

**Limitação conhecida.** Subcategorias e descrições que dependem de categoria ou subcategoria desativada falham com a
mensagem do módulo (o Contract de classificações não reativa categorias), então a reconstrução completa desses
casos exige reativar a categoria no destino antes. Vale o mesmo para logos (binários, fora do CSV).

## Importação de lançamentos ([#29](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/29))

`ITransactionImport` (`Imports.Contracts`) e o painel **Importar lançamentos** aceitam o arquivo `transactions` e, para ocorrências
recorrentes, o arquivo `recurring_series`. Qualquer outro formato (PDF, OFX, XLSX, CSV de banco) é rejeitado. O fluxo é o mesmo dos
cadastros: prévia sem gravar, depois confirmar ou cancelar; os arquivos não ficam armazenados. O imports só lê e traduz o CSV; as regras
são de Transactions (`IEntryImport`), executadas na mesma operação financeira.

**Referências.** Descrição, subcategoria, conta, cartão e final são resolvidos pelo `ExternalId` já existente (importe os cadastros antes);
nada é criado para corrigir o arquivo. Cadastros inativos, cartão sem final correspondente ou compra no cartão fora de classificação de despesa
são rejeitados pelas regras de Transactions. O `kind` do arquivo precisa coincidir com a classificação.

**Mês contábil e valores.** O mês de `accounting_month` e os centavos de `amount_cents` entram exatamente como informados; o cálculo
automático fora do cartão e a seleção de fatura da #89 não alteram o mês do arquivo. Faturas Fechadas ou Pagas continuam protegidas (a validação do cartão usa o guard de faturas) e a
importação não reabre nem altera pagamentos.

**Estado ([#88](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/88)).**
Qualquer receita ou despesa fora do cartão pode ser Pendente (`is_effective=false`,
sem `effective_date`) ou Paga/Recebida (`is_effective=true`, com a data da baixa).
Compras no cartão continuam sempre pagas na data da compra, sem baixa manual.
Os nomes internos e as colunas não mudam. Importar não cria pagamento de fatura.

**Parcelas.** O grupo precisa estar completo: as N parcelas, numeradas de 1 a N, mesmo cartão, data, descrição e classificação de despesa,
meses gravados preservados (inclusive antecipações da #89) e valores iguais à divisão oficial (a diferença de centavos fica na primeira). A compra e as parcelas mantêm os
identificadores do arquivo. Grupo incompleto ou inconsistente é rejeitado; nada é reconstruído por texto ou `(x/y)`.

**Recorrência.** Cada ocorrência aponta para a série (`recurring_entry_external_id`) e traz `occurrence_month`. A série vem do arquivo
`recurring_series`; para criar uma série nova, o arquivo de lançamentos precisa conter a primeira ocorrência (mês de `start_date`), que
deve coincidir com o modelo da série. Uma série já existente aceita apenas ocorrências novas e só se a definição for idêntica. Ocorrência
de série ausente é rejeitada.

**Reembolso.** Pessoa e percentual seguem as regras do formulário: juntos ou ambos vazios, percentual de
0 a 100 com até duas casas, só em despesa, e a pessoa precisa existir pelo `external_id`, sem inferência pelo nome;
por isso `people` é importado antes. O percentual do arquivo é o gravado, mesmo que o padrão da pessoa seja outro.
**Recebimentos não entram no CSV:** importar cria reembolsos Pendentes, e recuperá-los é papel do backup. Um lançamento
existente nunca é sobrescrito, portanto o que tem recebimento continua protegido.

**Idempotência e conflito.** Mesmo `ExternalId` e mesmos dados: idêntico, ignorado (nenhum lançamento ou efeito financeiro duplicado).
Mesmo `ExternalId` com dados diferentes, ou outra ocorrência no mesmo mês da série: conflito, arquivo rejeitado e nada é sobrescrito.

**Tudo ou nada.** Qualquer problema, em qualquer linha dos dois arquivos, desfaz o lote inteiro. Os problemas voltam com arquivo, linha e coluna.

**Fora do escopo desta versão (rejeitado ou não coberto).** Ajustes de fatura, transferências e pagamentos/cashbacks/descontos de fatura;
séries editadas com mudança de dia ou de mês contábil para "esta e as seguintes" (os deslocamentos da série não existem no Contract), de modo
que ocorrências futuras geradas depois da importação seguem a regra padrão da série; meses excluídos de uma série. As ocorrências do arquivo
são sempre fiéis; só a geração futura pode divergir nesses casos.

## Estado

Implementados: layout, escrita e leitura estrita, exportação de cadastros (#61) e de lançamentos e séries (#62), modelos e exemplos.
Importação de cadastros (#28) e de lançamentos (#29) implementadas.
