# Filtros salvos de lançamentos

A [#33](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/33)
permite salvar com um nome os critérios de uma consulta de lançamentos: categorias,
subcategorias, descrições, contas, cartões, finais, texto e, quando informada, a
situação Pendente/Efetivado. O período **não** é salvo: cada execução recebe o mês
contábil ou um intervalo de datas, então o mesmo filtro serve a qualquer mês.

Os critérios são listas (OU dentro de cada lista, E entre listas). O nome é único sem
distinguir caixa, acentos ou pontuação. Um filtro precisa de ao menos um critério.
Os filtros guardam o `ExternalId` dos cadastros, então continuam funcionando para
lançamentos existentes quando uma subcategoria é desativada.

Sem situação salva, o filtro não presume baixa: inclui pendentes e efetivados como
a consulta comum. Com situação Pendente, uma ocorrência efetivada sai do resultado
sem alterar as demais condições.

`ISavedFilterCatalog` (Contracts) lista, salva/renomeia/edita, exclui e executa
(`RunAsync`) o filtro, devolvendo lançamentos e totais pela mesma consulta de
`IEntryCatalog`, inclusive paginação. Para isso `EntryFilter` ganhou listas opcionais
(`AccountExternalIds`, `CardExternalIds`, `CardLastDigitsExternalIds`,
`CategoryExternalIds`, `SubcategoryExternalIds`, `MerchantExternalIds`). Ajustes de
fatura só entram quando não há critério de categoria, descrição ou final.

Os dados ficam em `transactions.saved_filters`, com listas `uuid[]`.

Na tela de Lançamentos, o painel Filtro salvo salva o filtro atual, aplica um filtro
sobre o mês/intervalo escolhido, renomeia/atualiza e exclui. Fora do escopo:
compartilhar filtros e agendar execução.
