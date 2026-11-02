# Finais de cartão

Origem: [issue #7](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/7).

**Estado:** implementado; persistido em [PostgreSQL](../architecture/persistence-postgresql.md)
(#60).
A reversibilidade de arquivamento da #53 foi aprovada para contas/cartões, sem ampliar
automaticamente o escopo para finais.

Em **Contas e cartões > Finais do cartão**, cadastrar ou editar quatro dígitos,
uso Próprio, Compartilhado ou Assinatura, e rótulo livre opcional.
O final é texto, preservando zeros à esquerda; aceita somente quatro caracteres de 0 a 9.
O mesmo final pode existir em cartões distintos, mas não se repete no mesmo cartão,
mesmo quando desativado.

## Decisões de design

- Entidade própria CardLastDigits, com ExternalId v7, auditoria UTC e tabela
  accounts.card_last_digits. Índice único por cartão e dígitos protege a duplicidade.
- O contrato ICardLastDigitsCatalog consulta finais por ExternalId do cartão,
  filtrando ativos por padrão e permitindo consulta histórica explícita.
  Cartão/conta inativos retiram os finais das seleções.
- Excluir oferece desativar com confirmação, preservando as referências existentes,
  sem depender do módulo Transactions. Reembolso não é regra deste cadastro.
- Tipo de uso inválido e cartão inexistente/inativo retornam falha esperada.
  Um final existente não pode ser transferido para outro cartão pela edição.

Testes cobrem os três tipos, zeros à esquerda, formatos inválidos, duplicidade,
edição, desativação e consulta histórica com PostgreSQL real.
