# Edição, exclusão e movimentação

A [#14](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/14)
acrescenta ações por lançamento. Editar preenche o formulário com os dados gravados,
com a fatura escolhida no cartão (#89). Fora do cartão não há escolha de mês: ele acompanha a data. Cancelar/limpar não salva. Excluir exige
confirmação; nas parcelas, edição e exclusão pedem escolher Somente esta ou Esta e
as futuras. O formulário não presume um alcance para uma parcela.

Os casos de uso verificam todas as origens e destinos pelo guard da #50 antes de
salvar. Falha em qualquer item desfaz a operação inteira, sem reabertura automática.
Mover somente uma parcela preserva as demais. No alcance futuro de parcelas, escolher outra fatura
desloca os meses correspondentes; campos não alterados preservam os valores
individuais, inclusive diferenças de centavos da primeira parcela.

Edição mantém a identidade dos lançamentos e os vínculos com a compra. Exclusão
parcial preserva a numeração original x/N e os vínculos restantes; ao excluir a
última parcela, a compra sem lançamentos também é removida. FKs locais impedem
parcelas órfãs. Nenhuma informação é reconstruída a partir de texto.

Referências inativas já gravadas podem ser mantidas ao corrigir outros campos do
histórico. Novas referências precisam estar disponíveis. Abertura e pagamentos de
fatura não são alterados: o saldo efetivo deriva dos lançamentos e é recalculado
naturalmente após editar/excluir um avulso Pago ou Recebido. A data efetiva só acompanha
uma alteração de data; editar outro campo preserva o registro da efetivação.

Selecionar uma linha habilita Duplicar selecionado, também disponível em Ctrl+D.
A cópia é um novo avulso, com data de hoje no fuso do negócio e os demais dados
de lançamento preservados, sem repetir a série de parcelas. A fatura de destino
continua sujeita à proteção normal.

A #89 recalcula o mês fora do cartão a cada alteração de data. No cartão, alterações
sem mudar data/cartão preservam o mês gravado de cada parcela, inclusive antecipadas.
Mudar data/cartão recalcula com Accounts; antecipadas rejeitam essas alterações pelo
guard de Invoices. A fatura pode ser escolhida explicitamente após o recálculo, sempre
Aberta. Alterar o calendário do cadastro nunca recalcula lançamentos existentes.
