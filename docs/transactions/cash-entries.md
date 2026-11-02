# Receitas e despesas avulsas

A [#11](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/11)
entrega entrada rápida em Lançamentos: data, descrição com sugestões, classificação,
valor sem sinal, conta, detalhes e mês contábil editável. É possível cadastrar uma
descrição sem sair da tela. Enter confirma o formulário; Ctrl+N limpa para o próximo
lançamento, mantendo a data. Selecionar descrição sugere sua subcategoria padrão,
que continua editável.

Accounts sugere o mês da data. Transactions persiste a escolha manual e o sinal
da classificação: receita positiva, despesa negativa. Transferência tem operação
própria. Valor zero/negativo, datas inválidas e cadastros ausentes/inativos são
rejeitados pelos casos de uso; não apenas pelo formulário.

Avulsos fora do cartão já nascem Efetivados com data efetiva e instante UTC da
confirmação. O saldo efetivo é calculado pela abertura de Accounts mais os movimentos
efetivados entre a referência inicial e a data consultada, inclusive. Movimentos
anteriores à abertura permanecem no histórico e no orçamento. Não há saldo mutável
duplicado. Cartões e pendências serão integrados nas #12 e #56.

A mesclagem de descrições transfere as referências dos lançamentos antes de remover
a origem, na mesma transação. As FKs locais preservam classificação e descrição;
a conta usa somente ExternalId, sem acesso cruzado ao schema Accounts.
