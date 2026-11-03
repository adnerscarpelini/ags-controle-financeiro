# Reembolsos

## Contexto

O reembolso era apurado por regras de rateio: filtro salvo, percentual e mês inicial
(#21/#22), com recebimento avulso por valor (#23). Mudar um filtro alterava meses
passados, e nenhum lançamento indicava de quem era o reembolso.

## Decisão

O reembolso é explícito: pessoa e percentual são informados na própria despesa. As
regras de rateio, a apuração por filtro e o recebimento avulso foram removidos (#78),
sem migração de dados, com autorização para descartar os dados de desenvolvimento.

**Estado:** cadastro de pessoas entregue (#78). Os demais passos estão nas issues
[#79](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/79) a
[#84](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/84).

## Alternativas

- **Manter as regras por filtro:** descartada pelos problemas acima.
