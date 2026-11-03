# Repetir lançamentos fora do cartão

Origem: [#91](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/91).

Em Mais detalhes, Repetir recebe de 2 a 120 vezes (padrão 2) e Dias, Semanas,
Meses ou Anos (padrão Meses). Fixa e Repetir são mutuamente exclusivas. Cartões
continuam usando parcelas, com divisão do total; repetições conservam o valor
inteiro em cada ocorrência. A primeira segue Pago/Recebido; as demais são Pendentes.

As datas são calculadas a partir da original, sem acumular ajustes de mês curto:
31/01 gera 28/02 e 31/03; 29/02 em repetição anual usa o último dia nos anos comuns.
O mês de referência vem da própria data. Limites do calendário são validados antes
de gravar. Criação e alterações são atômicas na operação financeira PostgreSQL.

EntryRepetition guarda quantidade e período. Cada lançamento guarda o vínculo e
a numeração; a tela compõe `(x/N)` sem alterar a descrição gravada, a busca ou os
relatórios. Exclusões não renumeram os registros restantes. Ao excluir o último,
o vínculo sem lançamentos também é removido.

Editar e excluir exigem alcance: Somente esta, Esta e as futuras ou Todas. Alcances
parciais não mudam ocorrências Pagas/Recebidas; somente Todas permite alterá-las.
A baixa continua individual, sem modificar o estado das outras ocorrências. Alterar
a data em um alcance coletivo desloca cada data pelo mesmo número de dias, conforme
decisão do usuário na #91; não refaz o calendário pelo período. Qualquer data fora
do intervalo suportado rejeita a operação inteira.

O CSV exporta cada ocorrência como lançamento comum e preserva seu estado. O vínculo
de repetição e sua numeração não fazem parte do layout; importar não reconstrói o grupo.
