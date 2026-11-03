# Receitas e despesas avulsas

A [#11](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/11)
entrega entrada rápida em Lançamentos: data, descrição com sugestões, classificação,
valor sem sinal, conta, detalhes e mês de referência editável. É possível cadastrar uma
descrição sem sair da tela. Enter confirma o formulário; Ctrl+N limpa para o próximo
lançamento, mantendo a data. Selecionar descrição sugere sua subcategoria padrão,
que continua editável.

Accounts sugere o mês da data. Transactions persiste a escolha manual e o sinal
da classificação: receita positiva, despesa negativa. Transferência tem operação
própria. Valor zero/negativo, datas inválidas e cadastros ausentes/inativos são
rejeitados pelos casos de uso; não apenas pelo formulário.

A [#88](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/88) permite
escolher Pago (despesa) ou Recebido (receita), ligado por padrão e reiniciado em cada
novo lançamento. Desligado, o lançamento nasce Pendente: entra na previsão e nos
totais do mês, sem movimentar o saldo atual. Passar a data apenas o torna atrasado.

Pagar/Receber abre confirmação com valor, data e conta editáveis. As alterações
valem só para o lançamento; a confirmação repetida não gera outro efeito. Ao editar
e desligar a chave, ou desfazer a baixa pelo contrato, ele volta a Pendente e o saldo
é restaurado. O saldo é derivado da abertura e dos lançamentos pagos/recebidos pela
data da baixa, sem acumulador duplicado. Cartões não têm chave nem baixa manual:
o caixa é afetado pelo pagamento da fatura. Receitas de reembolso continuam Recebidas.

A mesclagem de descrições transfere as referências dos lançamentos antes de remover
a origem, na mesma transação. As FKs locais preservam classificação e descrição;
a conta usa somente ExternalId, sem acesso cruzado ao schema Accounts.
