# Séries mensais e ocorrências

A [#16](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/16)
acrescenta Repetir todo mês no dia X ao próprio formulário. O dia vem da data inicial;
o término opcional é o mês da agenda, inclusive. Sem término, a série continua ativa.
Em meses curtos, usa o último dia sem perder o dia configurado para os seguintes.

RecurringEntry guarda os dados do plano. Cada ocorrência tem identidade própria,
vínculo com a série e OccurrenceMonth, a chave do mês da agenda, distinta do mês
contábil gravado. Índice único impede duplicação de uma ocorrência, inclusive em
consultas simultâneas. As consultas mensais e por intervalo geram automaticamente
os períodos necessários; sem um período futuro escolhido, a visão mostra o período
atual e os registros já conhecidos. Para planejar o futuro, consulte um mês ou um
intervalo com data final. Não se tenta gerar uma série infinita inteira.

Accounts calcula o mês contábil de cada data. A escolha manual da primeira
ocorrência fica nessa ocorrência; as seguintes seguem o calendário. A consulta
de fatura por mês inclui suas ocorrências e compõe também as faturas futuras já
registradas, como as que contêm parcelas.

Ocorrências de caixa, de receita ou despesa, nascem Pendentes mesmo quando a data
já passou. Futuras aparecem como previstas. Passar o tempo não registra baixa,
não cria outro lançamento e não movimenta saldo efetivo. O orçamento inclui os
valores previstos; efetivar uma ocorrência será a operação específica da #56.
Compras recorrentes no cartão seguem a composição da fatura e não debitam caixa.

Planos existentes preservam suas referências mesmo após desativação cadastral;
isso não autoriza selecionar inativos para criar um novo plano. Mesclar descrições
transfere também o cadastro da série, respeitando o bloqueio das compras existentes.

## Faturas protegidas e retorno após o corte

Nunca são incluídas ou alteradas ocorrências em fatura Fechada/Paga. O gerador
informa quantas foram protegidas e preserva a composição congelada. O fechamento
materializa antes os planos daquele mês, na mesma operação financeira.

Se o sistema estava desligado, um plano registrado **antes** do corte ainda pode
ser materializado em uma fatura que continua Aberta, antes de fechá-la. Isso
representa o plano já existente, sem permitir uma compra avulsa nova após o corte.
Uma série criada após o corte não usa esse caminho; uma fatura já Fechada/Paga
nunca o permite. As verificações e gravações usam a mesma transação da #50.

Editar/excluir ocorrências exige o caso de uso de alcance da #17. A edição
genérica de avulsos não serve como atalho para modificar a recorrência.
