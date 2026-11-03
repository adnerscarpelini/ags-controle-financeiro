# Séries mensais e ocorrências

A [#16](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/16)
acrescenta Repetir todo mês no dia X ao próprio formulário. O dia vem da data inicial;
o término opcional é o mês da agenda, inclusive. Sem término, a série continua ativa.
Em meses curtos, usa o último dia sem perder o dia configurado para os seguintes.

RecurringEntry guarda os dados do plano. Cada ocorrência tem identidade própria,
vínculo com a série e OccurrenceMonth, a chave do mês da agenda, distinta do mês
de referência gravado. Índice único impede duplicação de uma ocorrência, inclusive em
consultas simultâneas. As consultas mensais e por intervalo geram automaticamente
os períodos necessários; sem um período futuro escolhido, a visão mostra o período
atual e os registros já conhecidos. Para planejar o futuro, consulte um mês ou um
intervalo com data final. Não se tenta gerar uma série infinita inteira.

Accounts calcula o mês de referência de cada data (#89). Fora do cartão, ele é sempre
o mês da data. No cartão, a fatura escolhida na primeira
ocorrência fica nessa ocorrência; as seguintes seguem o calendário. A consulta
de fatura por mês inclui suas ocorrências e compõe também as faturas futuras já
registradas, como as que contêm parcelas.

A primeira ocorrência fora do cartão segue a chave Pago/Recebido, ligada por padrão
([#88](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/88)). As seguintes
nascem Pendentes, mesmo quando a data já passou. Futuras aparecem como previstas. Passar o tempo não registra baixa,
não cria outro lançamento e não movimenta saldo efetivo. O orçamento inclui os
valores previstos; efetivar uma ocorrência usa a operação específica da #56.
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

## Edição e exclusão com alcance

A [#17](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/17)
exige escolher Somente esta, Esta e as seguintes ou Todas antes de salvar qualquer
campo ou excluir. Cancelar mantém os dados. A edição genérica de avulsos rejeita
recorrências, inclusive quando chamada diretamente por outro consumidor.

Somente esta mantém a chave original da agenda mesmo se a data mudar. Excluir
registra o mês excluído na definição; consultas posteriores não o recriam.
Esta e as seguintes encerra o plano anterior e cria outro a partir do mês da
ocorrência, mantendo as identidades e situações dos registros existentes.
Todas conserva ocorrências passadas já efetivadas e altera as demais. Excluir
Todas desativa o plano e conserva esse histórico confirmado.

O dia configurado é separado da data de uma ocorrência: fevereiro não transforma
um plano de dia 31 em plano de dia 28. A edição de recorrências não oferece alteração de mês (#89). Novas séries e edições
usam deslocamento de referência zero. A coluna e a leitura do deslocamento continuam para
a importação; não se corrige histórico. Exceções de fatura preservam o mês se data e
cartão não mudarem; nos demais casos o mês vem do calendário.
Se a nova data atravessa o mês, o plano mantém também esse deslocamento de data,
inclusive ao gerar períodos futuros ou consultar por intervalo. Início e término
consultados refletem as datas deslocadas; a chave da agenda permanece estável.
Uma exceção manual de faturamento não vira configuração de todos os meses.

Origem e destino de todas as ocorrências afetadas precisam permitir a alteração.
Uma fatura protegida faz a operação inteira falhar, com rollback, sem reabertura
ou atualização parcial. Editar a configuração preserva a situação e a confirmação
de cada ocorrência; nunca registra uma baixa.

## Baixa manual por ocorrência

A #88 amplia a baixa manual da #56 a qualquer lançamento fora do cartão. A confirmação
permite editar valor, data e conta somente da ocorrência selecionada, sem alterar a série.
A conta arquivada original continua selecionada; outra conta ativa pode ser escolhida.
Alterar valor respeita a proteção dos reembolsos já recebidos. Repetir a confirmação
retorna sucesso sem alterar a baixa original ou movimentar novamente o saldo.
Desligar a chave na edição desfaz a baixa da ocorrência e restaura o saldo derivado.
Cartões são rejeitados pelas operações de baixa e desfazimento.
