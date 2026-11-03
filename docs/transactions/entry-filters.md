# Filtros e totais dos lançamentos

A [#15](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/15)
entrega filtros combinados por mês de referência, intervalo da data original, conta,
cartão, final, categoria, subcategoria, texto e Pendente/Efetivado. São interseções,
aplicadas pelo mesmo contrato EntryFilter na tela e em consumidores internos.
Ctrl+F foca a busca. Limpar filtros restaura a consulta completa.

O rodapé totaliza receitas, despesas assinadas e saldo de **todo o filtro**, não
somente a página visível. A tela mostra 100 registros por página; o contrato aceita
de 1 a 200. Ordem estável por data e identidade interna, sem expor essa identidade.
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
