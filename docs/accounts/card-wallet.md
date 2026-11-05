# Carteira visual de cartões

Implementada na [#55](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/55), em **Cadastros → Contas e cartões**.

`CreditCardView` desenha o cartão com HTML/CSS e DTOs: logo/iniciais e nome do banco, conta vinculada, bandeira, nome do cartão e finais ativos com seus rótulos. Não pede número completo, CVV nem imagem do cartão. O desenho acompanha as edições do cadastro; alterações no painel de finais notificam a página para atualizar a carteira.

A face tem proporção aproximada de 1,6:1, podendo crescer para nomes longos e vários finais. Abaixo ficam situação, limite cadastrado, fechamento, vencimento e regra do dia de fechamento. Esse limite não representa crédito disponível descontando compras. Editar, Finais do cartão e Arquivar/Desarquivar são botões nativos sempre visíveis, com foco e alvo mínimo de 44 pixels; não dependem de hover.

A grade adapta o número de colunas à largura. Tema, fonte local e tokens existentes são reutilizados, sem gradiente, sombra pesada ou CDN. Cores institucionais ficam no texto; o logo é a imagem local da #54, exibida sem distorção. Nome e iniciais preservam a identificação sem logo.

A consulta padrão mostra apenas cartões disponíveis. **Exibir arquivados e cartões indisponíveis pela conta** mantém a distinção entre arquivamento individual e conta arquivada. Ações passam pelos mesmos casos de uso e confirmação da #53, preservando histórico, saldos e faturas.

O componente recebe dados e callbacks, sem acessar banco ou executar regras financeiras. A página consulta os Contracts e coordena os editores; não há imagem estática duplicando o cadastro.

## Validação visual

Conferência em navegador a 1366 × 900 e 360 × 800, com três cartões fictícios, logo/iniciais, nome longo e limite de R$ 1.234.567,89. O HTML foi renderizado pelos componentes reais `MainLayout` e `AccountsAndCards`, com PostgreSQL isolado, e servido estaticamente para avaliar CSS sem alterar a confiança do certificado local. A página não apresentou rolagem horizontal; as ações mediram 44 pixels de altura e exibiram foco ao navegar com Tab. A face cresce quando o conteúdo exige, preservando nomes e ações.

Edição, atualização dos finais, callbacks e arquivamento foram exercitados em bUnit; esta conferência estática não comprova um circuito Blazor interativo no navegador nem uso em celular físico na LAN.

## Tela de Cartões (#100)

[#100](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/100): `/cartoes` mostra a carteira em cards, com as abas **Faturas abertas** e **Faturas fechadas**. Cada card traz situação, total, vencimento, barra de limite e a ação conforme o estado: **Pagar fatura** ou "Fatura paga". Na aba de abertas, uma fatura já fechada e não paga do cartão aparece em destaque, com o mesmo **Pagar fatura**. Clicar no card abre a fatura.

No cabeçalho, o nome do cartão quebra somente entre palavras. O selo da fatura fica ao lado do nome quando cabe e, senão, desce para a linha de baixo; o card de conta segue a mesma regra com o selo "Arquivada" ([#137](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/137)).

O menu ⋮ do card traz Editar, Finais do cartão, Histórico de faturas e Arquivar. **Ver cartões arquivados** mostra os arquivados e os indisponíveis pela conta. O resumo lateral mostra a próxima fatura a vencer, o limite disponível e o valor total das faturas.

O limite usado vem de `IInvoiceCatalog.GetCardLimitsAsync`: saldo restante das faturas ainda não pagas, inclusive parcelas futuras. Crédito de uma fatura não reduz outra.
