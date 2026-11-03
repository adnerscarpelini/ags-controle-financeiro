# Planejamento mensal

A [#94](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/94) implementa o orçamento mensal em
`/planejamento`. A [visão de 19 meses](planning.md) continua em `/relatorios/visao-19-meses`, acessível pelo
menu Relatórios, com os mesmos cálculos e totais.

## Definir e copiar

Sem planejamento no mês, aparecem **Definir novo planejamento** e **Copiar planejamento do mês anterior**.
A definição tem três etapas:

1. Renda mensal e percentual de economia (padrão 20%). Renda zero é válida; negativa é rejeitada.
   Percentual entre 0 e 100, inclusive. Orçamento = renda × (1 − percentual ÷ 100), arredondado em centavos
   por `Money`; valor economizado = renda − orçamento. As duas parcelas sempre somam a renda: com renda
   de R$ 0,01 e 50%, orçamento de R$ 0,01 e economia de R$ 0,00. Renda de R$ 1.000,00 e 20% produz orçamento
   de R$ 800,00 e economia de R$ 200,00.
2. **Gerenciar categorias** seleciona categorias de despesa e a meta não negativa de cada uma.
   Total categorizado = soma das metas; falta categorizar = orçamento menos essa soma. Metas podem
   ultrapassar o orçamento: o restante fica negativo. Uma meta de R$ 200,00 no orçamento de R$ 800,00
   deixa R$ 600,00 sem categorização.
3. Cartão pela **data da fatura** (padrão) ou pela **data da compra**.

A prévia é calculada pelo contrato, sem gravar. Há apenas um planejamento por mês. Copiar usa o mês
imediatamente anterior, inclusive na virada do ano, preserva renda, percentual, metas e visualização,
e cria identidades novas. Se a origem não existir ou o destino já tiver planejamento, informa a falha
sem gravar. Editar ou excluir a cópia não altera a origem. Cancelar a definição não grava nada.

## Consulta e valores

As setas navegam por mês. A tabela soma as subcategorias por categoria e exibe meta, despesas pagas,
previstas, total e progresso. **Pagas** usa `IsEffective`: Pagos fora do cartão e compras no cartão.
**Previstas** usa Pendentes. Total = pagas + previstas; restante = meta − total. A barra distingue
pagas e previstas e é limitada visualmente ao orçamento. Excedentes usam o token `Expense` e o texto
**Excederam**, sem depender apenas da cor.

Com meta de R$ 200,00, R$ 296,66 pagos e R$ 50,00 previstos, o total é R$ 346,66 e excederam R$ 146,66.
**Categorias restantes** agrupa o orçamento não categorizado e os gastos em categorias fora do plano.
A busca filtra as linhas pelo nome, sem alterar o resumo. É possível adicionar, editar e remover metas,
editar o planejamento, trocar a visualização do cartão e excluir o planejamento com confirmação.
Remover meta ou plano não altera lançamentos.

Fora do cartão, o mês é sempre o da data. No cartão, a visualização por fatura usa o mês gravado;
por compra, usa o mês de `Date`. Uma compra de novembro com fatura de dezembro conta em dezembro
na primeira opção e em novembro na segunda. **Pagamentos de fatura, transferências e movimentos
de conta não entram**, pois as compras já estão item a item. Ajustes de fatura entram uma única vez
pelo mês gravado, como no Ledger. As recorrências do mês consultado são garantidas pelo contrato de
Transactions; na visão por compra, também se garante o intervalo de datas do mês, mesmo que a
fatura vença no mês seguinte. Falha de geração é informada, sem apresentar totais incompletos.

Os cards mostram:

- Receitas do mês: somente **Recebidas**, excluindo Pendentes.
- Gastos planejados: orçamento de gastos do plano.
- Balanço planejado: receitas Recebidas menos gastos planejados.
- Economia planejada: balanço dividido pelas receitas Recebidas, em percentual; zero se não houver receita.

Receitas de R$ 200,00 e orçamento de R$ 800,00 dão balanço de R$ -600,00 e economia de -300,00%.
O percentual do card compara o resultado real com o orçamento; não é o percentual configurado na definição.

## Persistência, interface e testes

[Transactions guarda o agregado](../architecture/module-structure.md#planejamento-mensal-94), no schema
`transactions`; Reports permanece somente leitura. Valores monetários usam `Money` e `BIGINT` em centavos.
Validações ou falhas de persistência não deixam o plano parcialmente gravado.

A interface usa tokens dos temas, adapta tabela e cards ao celular e segue o [ocultar valores](../web/hide-values.md):
textos monetários e atributos acessíveis são mascarados pelo script global, inclusive após mudanças de mês;
campos de edição e percentuais seguem as exceções já documentadas nessa preferência.

`EntryTests.MonthlyPlans` cobre cadastro, cópia, edição, exclusão, falhas, atomicidade, centavos e limites.
`MonthlyPlanReportTests` cobre os valores de aceite, cartão nas duas visualizações, quitação sem duplicação,
recorrência, ajustes e receitas Recebidas. `PlanningPageTests` cobre o fluxo mensal; a visão preservada segue
em `PlanningTests` e `CategoryPlanningReportPageTests`. `PrivacyToggleTests` inclui as duas rotas autenticadas.
