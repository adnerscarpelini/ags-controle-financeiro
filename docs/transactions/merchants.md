# Descrições de lançamento

A [#10](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/10)
entrega cadastro, edição, busca, sugestões, desativação e mesclagem em
Cadastros > Descrições. A subcategoria padrão é opcional e precisa estar ativa.

A comparação de nomes usa decomposição Unicode, remove acentos, espaços e
pontuação e ignora caixa. Assim, `Loja Exemplo` e `LOJA-EXEMPLO` são equivalentes.
Duplicidades indicam o cadastro existente; um índice único protege gravações
concorrentes. Busca e sugestões usam a mesma normalização, por parte do nome.

Excluir oferece desativação com confirmação para preservar histórico. Mesclar
remove a origem e mantém a identidade do destino ativo numa transação. A migração
inicial deste cadastro precede os lançamentos: a transferência das referências
será integrada ao criar lançamentos (#11), e a proteção das faturas na #50.
