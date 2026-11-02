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
um plano de dia 31 em plano de dia 28. Alterações de mês contábil da série guardam
um deslocamento em relação ao calendário, aplicado também aos meses ainda não
materializados. Exceções que não foram alteradas conservam os seus campos.
Se a nova data atravessa o mês, o plano mantém também esse deslocamento de data,
inclusive ao gerar períodos futuros ou consultar por intervalo. Início e término
consultados refletem as datas deslocadas; a chave da agenda permanece estável.
Uma exceção manual de faturamento não vira configuração de todos os meses: o
deslocamento solicitado é aplicado ao plano, preservando as exceções individuais.

Origem e destino de todas as ocorrências afetadas precisam permitir a alteração.
Uma fatura protegida faz a operação inteira falhar, com rollback, sem reabertura
ou atualização parcial. Editar a configuração preserva a situação e a confirmação
de cada ocorrência; nunca registra uma baixa.

## Baixa manual por ocorrência

A [#56](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/56)
permite confirmar a baixa com a data efetiva, sugerindo hoje no fuso do negócio.
Valor, conta e mês contábil vêm da ocorrência. Uma conta arquivada permanece como
origem, identificada na confirmação; não é trocada silenciosamente.
A confirmação atualiza a ocorrência existente e grava data e instante de auditoria.
Repetir a confirmação retorna sucesso sem alterar a baixa original ou movimentar
novamente o saldo. Cartões são rejeitados por essa operação.

O saldo usa a data efetiva e o estado da própria ocorrência, na mesma transação:
não há lançamento de caixa duplicado nem acumulador que possa ficar divergente.
Ocorrências efetivadas ainda podem ser editadas/excluídas pelo alcance autorizado,
recalculando o saldo derivado. Todas preserva o passado já confirmado.
