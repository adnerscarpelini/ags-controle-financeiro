# Consistência financeira entre módulos

## Estado e contexto

**Decisões aprovadas; implementações acompanhadas nas issues referenciadas.**
O calendário de compras (#8), a abertura de contas (#47), lançamentos avulsos (#11/#12)
e o ciclo de faturas (#18/#50) estão implementados. Séries mensais e ocorrências (#16)
também estão entregues, com edição por alcance (#17), baixa manual (#56),
transferências (#46), ajustes de saldo (#48) e consulta de pendências (#57).
O arquivamento reversível também está implementado (#53). Outras visões
financeiras continuam nas respectivas issues.
Esta nota registra as fronteiras entre conceitos; os critérios de aceite permanecem nas issues.

## Datas, orçamento e caixa

Fora do cartão, o mês de referência é sempre o mês da data, recalculado na edição (#89).
No cartão, o formulário mostra a fatura calculada por Accounts e permite escolher
outra Aberta. Se a calculada estiver protegida, Invoices sugere a próxima disponível,
com aviso; a proteção pelo corte permanece mesmo sem estado gravado (decisão do
usuário na #89). Edições de detalhes preservam a fatura; mudar data/cartão recalcula.
Parcelas antecipadas mantêm o mês e não aceitam alteração de data/cartão. Alterações
no calendário cadastrado não recalculam histórico. CSV preserva o mês do arquivo.
Mover compras exige origem e destino Abertos (#50).

Pendente/Pago ou Recebido é estado do lançamento ou da ocorrência, não da conta nem da série.
Fora do cartão, a chave Pago/Recebido nasce ligada e determina o estado inicial (#88).
Na recorrência, só a primeira ocorrência segue a chave; as seguintes nascem Pendentes.
A baixa permite editar valor, data e conta só da ocorrência e pode ser desfeita.
A passagem da data deixa uma ocorrência atrasada, sem baixa automática (#16/#56).
Efetivar só uma ocorrência não modifica a série nem os meses seguintes.

Saldo efetivo da conta, previsão de caixa e saldo do orçamento mensal são conceitos distintos.
Pendências não movimentam saldo efetivo. Recebimento efetivado acrescenta caixa e despesa
efetivada o reduz. Compras de cartão compõem despesas/faturas; o caixa da conta é afetado
pelo pagamento da fatura, sem nova despesa (#19/#51).

A visão completa de pendentes (#57) inclui receitas e despesas. O dashboard (#58) usa a data
prevista entre hoje e hoje + (N − 1) dias, com N escolhido nas preferências (#69; padrão 7), e com atrasados separados, no fuso de negócio configurado.
O seletor de mês de referência não desloca essa janela.

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
  Informar o total correto gera lançamento da diferença no mesmo mês de referência, afetando despesas.
  Não reabre a fatura, altera compras originais ou modifica pagamentos; não é InvoicePayment.

A #19 implementa a política aprovada de abatimentos: criar, editar e excluir
Cashback/Desconto é permitido em Aberta/Fechada e bloqueado em Paga, inclusive na
origem e no destino de uma mudança. Pagamentos confirmados permanecem irreversíveis.
O débito atômico e a quitação explícita da #51 estão implementados; pagamentos
confirmados não podem ser editados ou excluídos pelos comandos de abatimento.

## Reembolsos (#78–#83)

Reembolso é indicado na despesa, por pessoa e percentual, e separado da baixa dela: uma despesa
Pendente já entra nos reembolsos do mês de referência. Receber não altera a despesa, a baixa nem a
fatura, então uma compra em fatura Fechada ou Paga pode ser recebida. O recebimento cria uma
receita avulsa Recebida por pessoa, com crédito único em conta. Receita e despesa continuam
brutas nos relatórios, sem compensação. Ver [Reembolsos](reimbursements.md).

## Recorrentes do cartão na fatura (#69)

Em fatura Aberta, a ocorrência recorrente do cartão com data posterior a hoje é **prevista**. Pela preferência,
ela soma no total só na data da cobrança (padrão, como o banco mostra) ou desde já. Fechada e Paga sempre somam tudo.
É uma escolha de apresentação e de cálculo de total e saldo: não altera os lançamentos gravados, as despesas do
mês nem as regras de quitação. O ajuste de total em fatura Aberta compara com o lançado até hoje.
Detalhes em [recorrentes previstas na fatura](../invoices/recurring-forecast.md).

## Recorrência, histórico e arquivamento

Editar valor, dia/data ou excluir uma ocorrência exige diálogo de alcance (#17):
Somente esta, Esta e as seguintes ou Todas, respeitando a proteção do histórico já confirmada
e o bloqueio de faturas. A baixa manual é uma ação distinta e atua só na ocorrência.

Arquivar contas/cartões (#53) oculta cadastros e novas seleções; desarquivar restaura a mesma
identidade. Não apaga nem esconde histórico, relatórios ou faturas pendentes.
O fluxo reversível usa o mesmo IsActive da desativação existente e distingue a
flag individual do cartão da disponibilidade derivada da conta.
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

## Ajuste específico de fatura (#52)

Invoices define o guard de ajuste, que permite Aberta/Fechada e bloqueia Paga.
Transactions conserva o registro de diferença no orçamento, sem FK entre módulos,
sem descrição fictícia e sem modificar a compra original. O mesmo limite
transacional protege leitura do total, validação do estado e gravação; a prévia
pode exigir o total esperado para não confirmar uma diferença desatualizada.
