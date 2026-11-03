# Arquivar e desarquivar contas e cartões

A [#53](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/53)
transforma a desativação existente em arquivamento reversível. O mesmo IsActive
continua sendo a única situação persistida; não há uma segunda flag contraditória.
Arquivar e desarquivar exigem confirmação no cadastro. Cancelar não altera dados.

A lista padrão e novas seleções mostram apenas cadastros disponíveis. Exibir
arquivados também revela cartões indisponíveis porque sua conta está arquivada.
O contrato distingue IsActive do cartão, IsAccountActive e IsAvailable derivado.
Não altera automaticamente a flag individual dos cartões ao arquivar uma conta.
Desarquivar a conta, portanto, não restaura cartões arquivados individualmente.

Reativar um cartão de uma conta ainda arquivada conserva sua identidade e flag
individual, mas ele continua indisponível até a conta ficar ativa. A tela distingue
Arquivado individualmente de Indisponível: conta arquivada. IDs inexistentes falham;
repetir a operação não cria cadastros. Nomes, dados e vínculos são preservados.

Histórico, relatórios, pendências, faturas, pagamentos e saldos usam consultas
históricas e permanecem visíveis, sem mudar valores ou situação de fatura.
A regra não se estende a bancos, categorias ou finais de cartão.
