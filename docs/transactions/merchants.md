# Descrições de lançamento

A [#10](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/10)
entrega cadastro, edição, busca, sugestões, desativação e mesclagem em
Cadastros > Descrições. A subcategoria padrão é opcional e precisa estar ativa.

A comparação de nomes usa decomposição Unicode, remove acentos, espaços e
pontuação e ignora caixa. Assim, `Loja Exemplo` e `LOJA-EXEMPLO` são equivalentes.
Duplicidades indicam o cadastro existente; um índice único protege gravações
concorrentes. Busca e sugestões usam a mesma normalização, por parte do nome.

Excluir oferece desativação com confirmação para preservar histórico. Mesclar
remove a origem e mantém a identidade do destino ativo numa transação. A mesclagem
transfere as referências dos lançamentos na mesma transação (#11). A proteção
da #50 rejeita integralmente uma mesclagem que alcance compras em fatura protegida.

## Tela de Descrições (#101)

`/cadastros/descricoes` abre na tabela, com busca pelo nome. A janela de criar e editar mostra as descrições parecidas já cadastradas. **Mesclar com outra** abre uma janela própria, que explica o efeito e pede a descrição a manter.
