# Receitas e despesas avulsas

A [#11](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/11)
entrega entrada rápida em Lançamentos: data, descrição com sugestões, classificação,
valor sem sinal, conta, detalhes e mês de referência editável. É possível cadastrar uma
descrição sem sair da tela. Enter confirma o formulário; "Salvar e criar nova" limpa para o
próximo lançamento, mantendo a data e a conta. Selecionar descrição sugere sua subcategoria padrão,
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

## Janela de lançamento (#97)

O cadastro abre em janela, pelo **+ Novo** de qualquer tela, sem sair dela, por `Components/Entries/EntryDialog.razor`:

- O valor recebe o foco e fica em destaque, na cor do tipo; a cor nunca é o único sinal, porque o título também diz o tipo.
- Data rápida com Hoje, Ontem e outra data. Campos principais: descrição, categoria e subcategoria, conta e a chave Pago/Recebido.
- **Mais detalhes** abre uma segunda coluna com fixa, repetir, reembolso, observação e anexo. Recolher não perde o que foi digitado.
- O campo **Categoria e subcategoria** mostra `Categoria › Subcategoria`, agrupado por categoria e só do tipo do lançamento, e grava a subcategoria.
- **Salvar** e **Salvar e criar nova** ficam indisponíveis até o preenchimento mínimo, com a explicação do que falta. Erros de regra aparecem na janela e mantêm os dados.
- **Salvar e criar nova** limpa o formulário e mantém a data e a conta ou o cartão, para lançar vários em sequência.
- Editar usa a mesma janela. Em séries e parcelas ela pede o alcance da alteração.
- Fechar com dados preenchidos, pelo X ou pelo Esc, pede confirmação. Clicar fora não fecha.

Nenhuma regra fica no componente: mês, parcela e fatura continuam nos casos de uso, por `IEntryCatalog` e `IInvoiceCatalog`.
