# Operações financeiras entre módulos

## Contexto e decisão

O fechamento precisa ser atômico com a proteção das compras (#50), assim como
pagamento/débito (#51) e antecipação/desconto (#32). Manter validação e gravação
em transações independentes deixaria uma janela de concorrência.

Accounts publica IFinancialOperation, um limite técnico pequeno para executar
uma operação com seu critério explícito de sucesso. A implementação PostgreSQL
usa TransactionScope com fluxo assíncrono, isolamento ReadCommitted e um advisory
lock transacional para serializar as mutações financeiras no banco. Cada módulo
continua dono de suas tabelas e DbContext; não há JOIN, FK ou SQL entre schemas.

Accounts, Transactions e Invoices compartilham uma instância NpgsqlDataSource.
Os contextos são curtos, usados e fechados sequencialmente: nunca há conexões
abertas simultâneas dentro da operação. O Npgsql reutiliza a conexão alistada
nesse cenário, evitando transação distribuída; ver a
[documentação oficial](https://www.npgsql.org/doc/basic-usage.html#systemtransactions-and-distributed-transactions).
Um retorno de falha ou exceção deixa o escopo sem Complete e desfaz tudo.
Não paralelizar chamadas de persistência dentro desse limite.
Consultas de lançamentos, saldo e faturas que reúnem referências de várias tabelas
também usam esse limite: uma gravação concorrente não pode introduzir uma parcela
cuja compra ainda não apareça no conjunto de referências já lido pela consulta.

Transactions publica IInvoiceWriteGuard, implementado por Invoices e composto
na injeção de dependência. A validação de abertura ocorre dentro da mesma operação
da gravação. Não há referência inversa Transactions → Invoices nem ciclo. O guard
permissivo serve somente para o módulo Transactions isolado; o host registra todos
os módulos e usa a implementação de Invoices.

Alternativas descartadas: validação somente na tela não protege concorrência;
duplicar a situação da fatura em Transactions exigiria sincronizar duas fontes;
transações distribuídas acrescentariam dependências de plataforma e recuperação.
A serialização de todas as mutações é um custo aceito para uma instalação pessoal,
com um usuário. Não se introduz Repository genérico ou mensageria.

## Situação

Fundação da #50; pagamento e antecipação serão integrados nas #51 e #32. Testes
relacionais devem demonstrar rollback conjunto e ausência de promoção distribuída.
