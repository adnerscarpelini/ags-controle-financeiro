# Mês contábil de uma compra

Origem: [issue #8](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/8).

O contrato IAccountingMonthCalculator recebe DateOnly e, opcionalmente, o ExternalId
do cartão. Retorna uma sugestão de AccountingMonth ou falha esperada quando o cartão
não existe ou a fatura ultrapassa o intervalo de datas suportado.
Sem cartão, retorna o próprio mês da data.

## Calendário

1. Ajustar o fechamento ao último dia do mês quando necessário.
2. A compra pertence ao fechamento desse mês quando for anterior ao fechamento,
   ou no próprio dia se IncludesClosingDay estiver ligado.
3. Caso contrário, considerar o fechamento do mês seguinte, recalculando o último dia.
4. Ajustar também o vencimento ao último dia do mês do fechamento. Se cair antes
   do fechamento, o vencimento fica no mês seguinte; se coincidir, permanece no
   mesmo mês, conforme decisão confirmada na issue.
5. Retornar o mês do vencimento.

Exemplos: fechamento 3/vencimento 10, compra 14/09/2026 → 10/2026;
compra 03/10/2026 → 10/2026 incluindo o fechamento, ou 11/2026 excluindo.
Fechamento 31/vencimento 30, compra 20/02/2027 → 02/2027, pois ambos os
dias se ajustam a 28/02. Em dezembro, o cálculo avança corretamente o ano.

## Decisões de design

- O cálculo puro fica no Domain de Accounts; o SharedKernel mantém somente
  Money e AccountingMonth. Application busca o cartão e traduz falhas para Contracts.
- Usa DateOnly: nenhuma dependência de fuso horário ou horário de processamento.
- Cartões desativados continuam permitindo cálculo para dados históricos, embora
  não apareçam nas seleções de novos lançamentos.
- O serviço não grava nem move lançamentos. O resultado é editável no cadastro
  (#12) e depois de salvo (#14); a fatura escolhida manualmente será preservada.
- Testes cobrem cada exemplo dos critérios, inclusão/exclusão do dia de fechamento,
  fevereiro comum/bissexto, meses de 30 dias, vencimento ajustado, virada de ano,
  coincidência dos dias e limite máximo de DateOnly. Integração testa o calendário
  persistido e a consulta pelo contrato.
