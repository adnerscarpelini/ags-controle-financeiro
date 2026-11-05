# Pendências da janela do dashboard e atrasadas

A [#58](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/58) acrescenta
ao dashboard o que há a receber e a pagar no curto prazo (`IPendingAgenda`, em
`Reports.Contracts`).

**Janela.** Intervalo fechado de hoje até hoje + (N − 1) dias, com N = 7, 15 ou 30 conforme a
[preferência](../preferences/settings.md) (padrão 7, os "próximos sete dias" originais da #58), pela **data prevista** do
lançamento, não pelo mês de referência nem pela criação. Atravessa a virada do ano e inclui
29/02 em ano bissexto. Atrasados são os de data prevista anterior a hoje, em lista e
totais separados, fora dos totais da janela.

**Hoje.** Vem de `TimeProvider.GetLocalNow()`, ou seja, do fuso de negócio configurado na
distribuição Docker (`TZ`, [#59](../web/docker-deployment.md)); não é a data UTC do container
nem a do navegador. A auditoria continua em UTC. O seletor de mês de referência do dashboard não
desloca a janela: a seção não recebe o mês.

**Fonte.** Reports consome `IEntryCatalog.QueryPendingAsync` (a visão de pendentes da #57)
com paginação interna; não lê tabelas de Transactions. Mostra data, descrição, valor e
conta de cada item, e totais separados de a receber e a pagar. Efetivar uma ocorrência
(baixa manual) a tira da lista e dos totais. Sem pendências, a seção mostra estado vazio e
totais zerados.

**Lista ([#130](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/130)).** Cada card mostra 10 itens por vez, com navegação para frente e para trás quando
há mais que isso. Os totais continuam somando todos os itens da janela.

**Saldo.** São projeções; pendências não movimentam o saldo efetivo da conta (coberto por
teste). Saldo efetivo, previsão de caixa e orçamento mensal continuam rótulos distintos. A
seção liga à visão completa em `/pendentes`.

Fora do escopo: notificações, efetivação automática e previsões estatísticas.
