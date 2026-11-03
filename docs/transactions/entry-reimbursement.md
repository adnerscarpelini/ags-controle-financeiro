# Pessoa e percentual de reembolso em despesas

A [#79](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/79) permite
indicar, em uma despesa, a pessoa que vai devolver parte dela e o percentual.

**Formulário.** Os campos aparecem só em despesas. Escolher a pessoa pré-preenche o
percentual com o padrão dela, que pode ser alterado de 0 a 100, com até duas casas.
Trocar a pessoa preenche de novo; abrir para editar mostra o percentual gravado.

**Regras nos casos de uso.** Pessoa e percentual existem juntos ou não existem. A pessoa
precisa estar cadastrada. Receita com pessoa é rejeitada; para mudar uma despesa para
receita, é preciso remover a pessoa.

**Parcelas e cópias.** A compra replica pessoa e percentual em todas as parcelas. "Esta e
as seguintes" aplica só o campo alterado, mantendo exceções. Duplicar copia os dois
campos, e antecipar parcelas os preserva no novo mês contábil.

**Recorrentes ([#80](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/80)).**
A série guarda pessoa e percentual, e cada ocorrência copia a configuração vigente. A edição
segue a pergunta de alcance (#17): "Somente esta" cria exceção, "Esta e as seguintes" vale
desta em diante, e "Todas" preserva as ocorrências passadas já confirmadas. Só o campo
alterado é aplicado às outras ocorrências, e a baixa não muda pessoa, percentual nem série.

**Faturas.** Alterar pessoa ou percentual é edição da compra e segue o bloqueio de
faturas Fechadas e Pagas (#50).

O modelo está em [Reembolsos](../architecture/reimbursements.md).
