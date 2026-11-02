# Consistência financeira entre módulos

## Estado e contexto

**Decisões aprovadas; implementações acompanhadas nas issues referenciadas.**
O calendário de compras (#8), a abertura de contas (#47), lançamentos avulsos (#11/#12)
e o ciclo de faturas (#18/#50) estão implementados. Séries mensais e ocorrências (#16)
também estão entregues, com edição por alcance (#17), baixa manual (#56),
transferências (#46), ajustes de saldo (#48) e consulta de pendências (#57).
Pagamentos, ajustes de fatura, arquivamento reversível e outras visões financeiras
ainda têm entregas abertas.
Esta nota registra as fronteiras entre conceitos; os critérios de aceite permanecem nas issues.

## Datas, orçamento e caixa

O mês contábil calculado por Accounts é uma sugestão editável, conforme
[o calendário implementado](../accounts/purchase-accounting-month.md).
A escolha manual pertence a Transactions (#12/#14), preservando a data da compra.
Mover uma compra exige faturas de origem e destino Abertas (#50).

Pendente/Efetivado é estado do lançamento ou da ocorrência, não da conta nem da série.
Recorrentes fora do cartão nascem Pendentes; avulsos fora do cartão, Efetivados.
A passagem da data deixa uma ocorrência atrasada, sem baixa automática (#16/#56).
Efetivar só uma ocorrência não modifica a série nem os meses seguintes.

Saldo efetivo da conta, previsão de caixa e saldo do orçamento mensal são conceitos distintos.
Pendências não movimentam saldo efetivo. Recebimento efetivado acrescenta caixa e despesa
efetivada o reduz. Compras de cartão compõem despesas/faturas; o caixa da conta é afetado
pelo pagamento da fatura, sem nova despesa (#19/#51).

A visão completa de pendentes (#57) inclui receitas e despesas. O dashboard (#58) usa a data
prevista entre hoje e hoje + 6, com atrasados separados, no fuso de negócio configurado.
O seletor de mês contábil não desloca essa janela.

## Faturas, pagamentos e ajustes

- Situações exclusivas: **Aberta, Fechada e Paga** (#50). Pagamento parcial não cria outra situação.
- Fechamento automático pelo dia configurado, com opção de fechamento manual (#50).
  O corte usa meia-noite seguinte ao fechamento quando IncludesClosingDay=true, ou do próprio
  dia quando false, no fuso de negócio. Limites de mês seguem a regra aprovada na
  [implementação do ciclo](../invoices/lifecycle.md). A reabertura manual suprime novo
  fechamento automático até o usuário fechar manualmente.
- Compras em Fechada/Paga não podem ser editadas, excluídas ou movidas; novas compras também
  não entram nessas faturas por caminhos alternativos, como parcelas, recorrências ou CSV.
- Reabrir é manual e só é permitido enquanto não existir pagamento confirmado.
  Qualquer pagamento confirmado, inclusive parcial ou antecipado, impede reabrir depois do fechamento.
- Antecipar pagamento não fecha uma fatura Aberta por si só; ela ainda pode receber compras.
- Saldo restante zero ou negativo não muda a situação automaticamente para Paga,
  inclusive ao fechar ou ajustar o total. Exigir confirmação explícita de quitação,
  sem criar pagamento de valor zero/negativo para representar apenas a mudança de situação.
- Pagamentos confirmados são irreversíveis (#19/#51). A conta vinculada ao cartão vem
  pré-selecionada, podendo ser trocada por outra corrente ativa. Registro e débito precisam ser
  atômicos e idempotentes; nenhum deles gera uma segunda despesa.
- Transferências e pagamentos podem deixar a conta corrente negativa, conforme decisão
  confirmada nas #46/#51. Saldo insuficiente, por si só, não bloqueia essas operações.
- O ajuste específico de total (#52) é permitido em Aberta/Fechada e bloqueado em Paga.
  Informar o total correto gera lançamento da diferença no mesmo mês contábil, afetando despesas.
  Não reabre a fatura, altera compras originais ou modifica pagamentos; não é InvoicePayment.

Limites ainda não definidos, como edição de abatimentos após quitação e detalhes de correção
de ajustes, continuam nas respectivas issues antes da implementação. Esta nota não transforma
ausência de regra em comportamento automático.

## Recorrência, histórico e arquivamento

Editar valor, dia/data ou excluir uma ocorrência exige diálogo de alcance (#17):
Somente esta, Esta e as seguintes ou Todas, respeitando a proteção do histórico já confirmada
e o bloqueio de faturas. A baixa manual é uma ação distinta e atua só na ocorrência.

Arquivar contas/cartões (#53) oculta cadastros e novas seleções; desarquivar restaura a mesma
identidade. Não apaga nem esconde histórico, relatórios ou faturas pendentes.
A desativação existente é a base dessa evolução, mas o fluxo reversível ainda não foi entregue.
Não estender a decisão automaticamente a bancos, categorias ou finais de cartão.

## Troca de dados e consequências

Apenas CSV no layout oficial/versionado (#28/#29/#61/#62). Importação não contorna estados,
reabre faturas, sobrescreve histórico protegido ou efetiva pendências silenciosamente.
Exportação é somente leitura. Recuperação de desastre pertence ao
[backup PostgreSQL](persistence-postgresql.md), incluindo dados que não estejam no CSV.

Invoices coordena suas operações consumindo Contracts permitidos de Accounts/Transactions.
O bloqueio das alterações precisa funcionar nos casos de uso, não apenas no Blazor, sem criar
ciclo Transactions → Invoices → Transactions. A [operação financeira compartilhada](financial-operations.md)
e o guard definido em Transactions implementam essa proteção na #50, com rollback entre
DbContexts e conexões sequenciais na mesma transação PostgreSQL.
