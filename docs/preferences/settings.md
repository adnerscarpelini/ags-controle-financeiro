# Preferências do aplicativo

A [#69](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/69) cria o módulo
`Preferences` e amplia a tela `/configuracoes` com as parametrizações abaixo. Senha, backup e
certificado continuam na mesma tela.

## Armazenamento

Um registro por instalação (o aplicativo tem um único usuário), na tabela `user_preferences` do schema
`preferences`, com `DbContext` e histórico de migrations próprios. As preferências valem em todos os
dispositivos e entram no backup do banco; **não** fazem parte do CSV. Sem registro salvo, valem os padrões.

O módulo não depende de nenhum outro. Quem precisa de uma preferência a lê por `IPreferences`
(`Preferences.Contracts`): Invoices, Reports e o host Web. Transactions não consome o módulo: o
chamador informa o que precisa (tamanho da página, data limite do ajuste).

`IPreferences.SaveAsync` valida todos os campos antes de gravar. Valor fora das opções é recusado com
mensagem e o que estava salvo é mantido.

## Preferências

| Grupo | Preferência | Opções | Padrão |
| --- | --- | --- | --- |
| Aparência | Tema | Escuro, Claro, Automático | Escuro |
| Geral | Tela inicial | Dashboard, Lançamentos, Faturas | Dashboard |
| Faturas | Recorrentes do cartão na fatura aberta | Somar só na data, Somar desde já | Somar só na data |
| Dashboard | Janela de pendências | 7, 15 ou 30 dias | 7 |
| Dashboard | Maiores categorias no gráfico | 5, 10 ou 20 | 10 |
| Dashboard | Seções visíveis | Resumo, Pendências, Categorias, Faturas, Mapa de calor | Todas |
| Lançamentos | Itens por página | 25, 50, 100 ou 200 | 100 |
| Segurança | Duração da sessão sem "Manter conectado" | 12 h, 24 h ou 7 dias | 12 h |

## Tema

- Os tokens de `tokens.css` são `light-dark(claro, escuro)`. O atributo `data-theme` do `<html>`
  (`dark`, `light` ou `auto`) escolhe o `color-scheme`; no automático, o sistema operacional decide.
- O servidor lê a preferência ao renderizar `App.razor` e já entrega o atributo no HTML inicial, inclusive
  no login, para a página não piscar no tema errado. Sem conseguir ler a preferência, vale o escuro.
- Ao salvar, `js/theme.js` troca o atributo na hora, sem recarregar a página.
- O contraste de todos os textos (e do rótulo dos botões) é no mínimo 4,5:1 (WCAG AA) nos dois temas;
  `UiTokensTests` calcula isso a partir dos próprios tokens.
- A cor da barra do navegador (`theme-color`) acompanha o tema; ver [Ocultar valores e cor da barra](../web/hide-values.md).
- A página de manutenção do restore e `offline.html` são servidas fora da aplicação e continuam escuras.

## Recorrentes do cartão na fatura

Ver [Recorrentes previstas na fatura](../invoices/recurring-forecast.md).

## Demais preferências

- **Tela inicial:** `/` redireciona para a tela escolhida; os endereços diretos continuam funcionando.
- **Janela de pendências:** o intervalo é fechado, de hoje até hoje + (N − 1) dias. O título da seção
  acompanha o N. Ver [Pendências do dashboard](../reports/pending-agenda.md).
- **Maiores categorias:** muda só a quantidade do gráfico; a tabela de categorias continua completa.
- **Seções visíveis:** a seção desligada some do dashboard. Com todas desligadas, aparece um aviso com
  link para `/configuracoes`.
- **Itens por página:** tamanho da consulta de `/lancamentos` (também ao aplicar filtros salvos) e de `/pendentes` (#71). Os
  totais continuam somando todos os lançamentos do filtro.
- **Duração da sessão:** gravada no ticket do cookie no login, então vale a partir do próximo login e não
  altera sessões abertas. "Manter conectado" continua com 30 dias.

## Fora do escopo

Moeda/idioma/fuso, bloqueio de login e tamanho mínimo da senha, regras financeiras,
horário e retenção do backup (ficam na configuração do Compose, para não dependerem do banco que o
backup protege), porta e certificado, e preferências por cartão. Ocultar valores não é uma preferência do banco: é uma escolha por dispositivo, descrita em [Ocultar valores](../web/hide-values.md).
