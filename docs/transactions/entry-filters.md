# Filtros e totais dos lançamentos

A [#15](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/15)
entrega filtros combinados por mês de referência, intervalo da data original, conta,
cartão, final, categoria, subcategoria, texto e Pendente/Efetivado. São interseções,
aplicadas pelo mesmo contrato EntryFilter na tela e em consumidores internos.
Ctrl+F abre e foca a busca. Limpar filtros restaura a consulta completa.

Os cards totalizam receitas, despesas assinadas e balanço de **todo o filtro**, não
somente a página visível. A tela mostra 50 registros por página por padrão (preferência
#69); o contrato aceita de 1 a 200. Ordem estável por data e identidade interna, sem expor essa identidade.
Página além da última é ajustada para a última; intervalos invertidos são rejeitados.

Filtros estruturados e busca são aplicados no PostgreSQL, sem JOIN entre módulos.
Descrições usam sua chave normalizada; detalhes têm normalização de acentos/caixa
que preserva espaços e pontuação. Registros anteriores à coluna de busca recebem
tratamento de leitura compatível em C#, sem modificar os dados ao consultar.
Referências são compostas pelos Contracts na mesma operação de consulta consistente.

Os totais monetários usam decimal/Money e centavos persistidos, sem ponto flutuante.
São totais de orçamento do filtro, distintos do saldo efetivo da conta. A integração
das ocorrências pendentes é entregue nas #16/#56. Cadastros inativos continuam nas
opções financeiras; a seleção para novos lançamentos usa somente os disponíveis.

Valores sempre mostram sinal e tokens de receita/despesa. Mês futuro tem indicação
textual e token cinza. A paginação limita o trabalho de renderização mesmo com
10 mil lançamentos; o teste relacional mede o tempo da consulta filtrada, incluindo
totais e composição dos DTOs, exigindo menos de um segundo.

## Tela de Lançamentos (#98)

[#98](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/98): a consulta abre no mês atual, com a tabela logo abaixo dos cards.

- **Tipo:** Todas, Receitas, Despesas, Transferências e Pendentes. **Busca** e **Filtros** ficam recolhidos em botões com tooltip; o painel traz Todos os meses, De, Até, conta, cartão, final, categoria, subcategoria, tipo, situação e [filtros salvos](saved-filters.md). Filtros ativos viram etiquetas removíveis.
- **Cards:** Saldo atual, Receitas, Despesas e Balanço mensal. Receitas, Despesas e Balanço acompanham o período e os filtros. Zero não mostra sinal.
- **Período:** o seletor `< Novembro de 2026 >` muda cards e tabela juntos. Com De, Até ou Todos os meses, ele fica desligado.
- **Tabela:** seleção, Situação, Data, Descrição, Categoria (`Categoria › Subcategoria`), Conta ou cartão, Valor e Ações. A situação é um ícone com tooltip, como "Pago em 03/11/2026" ou "Pendente desde 12/12/2026".
- **Ordenação e paginação no servidor:** `EntryFilter.SortColumn` ordena por data, descrição, categoria e valor. Conta e cartão pertencem a outro módulo e não são ordenáveis, porque não há JOIN entre módulos.
- **Ações por linha:** Pagar/Receber (só pendente fora do cartão), Editar, Anexar, Duplicar, Antecipar (só em parcelas) e Excluir, em janelas.
- **Rodapé:** linhas por página, posição e **Saldo previsto do mês**, de `IEntryCatalog.GetBalanceForecastAsync`: saldo atual somado às pendências do mês, fora do cartão. A tela não recalcula saldos.
- **Atalhos:** Ctrl+N abre o menu Novo, Ctrl+F abre a busca e Ctrl+D duplica o selecionado.

## Agrupar cartões

A opção **Agrupar cartões** ([#129](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/129)) troca os lançamentos de cada cartão por uma linha por cartão e fatura. A escolha fica nas [preferências](../preferences/settings.md) e vale ao abrir a tela de novo.

- **A linha:** nome do cartão, mês da fatura, quantidade de lançamentos e a soma. A ação **Ver fatura** abre a fatura daquele mês, onde ficam os itens.
- **Cartão e mês separados:** com Todos os meses ou um período, o mesmo cartão tem uma linha por mês de fatura.
- **Filtros primeiro:** a soma considera só o que passou pelos filtros. Com uma categoria ou uma busca, ela pode ser menor que o total da fatura.
- **Ajustes de fatura** entram na soma do cartão.
- **Cards e totais não mudam:** o agrupamento altera só as linhas. O rodapé passa a contar linhas em vez de lançamentos.
- **Ordem e páginas:** o agrupamento vale para o resultado inteiro, antes da paginação, então a soma não depende da página. Nesse modo a consulta carrega todos os lançamentos do filtro e monta a página em memória. Na ordem por data, a linha usa a data do lançamento mais recente; na ordem por categoria, ela não tem categoria própria.
- **Pendentes** não muda: lançamentos de cartão não têm baixa e não aparecem nessa visão.

## Transferências e ajustes de saldo

A [#141](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/141) mostra em Lançamentos os movimentos de conta que não são receita nem despesa.

- **Linhas:** cada transferência vira duas linhas, a saída na origem (`Transferência para <destino>`, `-R$`) e a entrada no destino (`Transferência de <origem>`, `+R$`), com a categoria `Transferência`. Cada ajuste de saldo vira uma linha na conta ajustada, com a diferença e a categoria `Ajuste de saldo`. Os valores ficam em cor neutra, com sinal, e entram pela data do movimento.
- **Totais:** Receitas, Despesas e Balanço mensal não mudam. Saldo atual e Saldo previsto já consideravam esses movimentos.
- **Tipo:** `Todas` inclui os movimentos; `Transferências` mostra só eles, com totais zerados; `Receitas` e `Despesas` não os incluem. `Pendentes` inclui os meses pendentes de transferências recorrentes.
- **Filtros:** conta mostra só a linha daquela conta; cartão, final, categoria, subcategoria, tipo e reembolso escondem os movimentos, que não têm esses dados. Período, situação e busca valem normalmente; a busca ignora acentos e procura na descrição e na categoria. Um filtro salvo também mostra os movimentos que passam pelos critérios dele.
- **Ações:** um mês de transferência recorrente tem as mesmas ações da tela de Contas (Confirmar, Editar, Desfazer e Excluir), pelo componente compartilhado `RecurringTransferActions`. Transferência avulsa e ajuste não têm ações nem seleção.
- **Fora do escopo:** pagamentos de fatura, o CSV e os demais consumidores da consulta. Só a tela pede os movimentos, com `EntryFilter.IncludeAccountMovements`; `AccountMovementsOnly` corresponde ao chip.
- **Consulta:** os movimentos são montados em memória a partir de `AccountMovement` e `TransferOccurrence`, no mesmo módulo, e intercalados com a página do banco pela mesma janela usada nos ajustes de fatura. A ordem escolhida é refeita depois da intercalação; com o agrupamento de cartões, eles continuam como linhas próprias.
