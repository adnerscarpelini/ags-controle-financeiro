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

- **Tipo:** Todas, Receitas, Despesas e Pendentes. **Busca** e **Filtros** ficam recolhidos em botões com tooltip; o painel traz Todos os meses, De, Até, conta, cartão, final, categoria, subcategoria, tipo, situação e [filtros salvos](saved-filters.md). Filtros ativos viram etiquetas removíveis.
- **Cards:** Saldo atual, Receitas, Despesas e Balanço mensal. Receitas, Despesas e Balanço acompanham o período e os filtros. Zero não mostra sinal.
- **Período:** o seletor `< Novembro de 2026 >` muda cards e tabela juntos. Com De, Até ou Todos os meses, ele fica desligado.
- **Tabela:** seleção, Situação, Data, Descrição, Categoria (`Categoria › Subcategoria`), Conta ou cartão, Valor e Ações. A situação é um ícone com tooltip, como "Pago em 03/11/2026" ou "Pendente desde 12/12/2026".
- **Ordenação e paginação no servidor:** `EntryFilter.SortColumn` ordena por data, descrição, categoria e valor. Conta e cartão pertencem a outro módulo e não são ordenáveis, porque não há JOIN entre módulos.
- **Ações por linha:** Pagar/Receber (só pendente fora do cartão), Editar, Anexar, Duplicar, Antecipar (só em parcelas) e Excluir, em janelas.
- **Rodapé:** linhas por página, posição e **Saldo previsto do mês**, de `IEntryCatalog.GetBalanceForecastAsync`: saldo atual somado às pendências do mês, fora do cartão. A tela não recalcula saldos.
- **Atalhos:** Ctrl+N abre o menu Novo, Ctrl+F abre a busca e Ctrl+D duplica o selecionado.
