# Registro de erros inesperados

A [#107](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/107) cria o módulo `Diagnostics`, que guarda
erros inesperados na tabela `diagnostics.error_records` para análise posterior. Não há tela: a consulta é feita direto no banco.

```sql
SELECT "LastOccurredAt", "Occurrences", "ExceptionType", "Source", "SqlState", "StackTrace"
FROM diagnostics.error_records
ORDER BY "LastOccurredAt" DESC;
```

## O que é gravado

Um `ILoggerProvider` próprio recebe todo log `Error` ou `Critical` que traz exceção, inclusive os do ASP.NET Core, do circuito
do Blazor e de serviços de segundo plano. Os módulos usam apenas `ILogger`, sem depender do `Diagnostics`.

- **Guardado:** tipo, pilha de toda a cadeia de exceções (limitada a 64 mil caracteres), origem (categoria do log), `SqlState`,
  primeira e última ocorrência e contador.
- **Não guardado:** a mensagem da exceção, o texto do log e o `Detail` do PostgreSQL, que podem conter valores do usuário.
- **Repetições:** a impressão digital combina origem, tipo e pilha; o mesmo erro soma no contador, sem nova linha.
- **Retenção:** registros sem ocorrência há mais de 90 dias são apagados ao iniciar e uma vez por dia.

## O que fica de fora

- Logs sem exceção ou abaixo de `Error`.
- Cancelamento (`OperationCanceledException`) e circuito desconectado (`JSDisconnectedException`).
- Logs de categoria `Microsoft.EntityFrameworkCore`: o EF Core registra como erro até o conflito de restrição que o chamador
  trata. Um erro de banco que ninguém trata reaparece no log do host e é gravado.
- Comandos de terminal (`user`, `backup`, `attachments`): não sobem os serviços de segundo plano, então o erro fica só na saída
  do comando.

## Falhas na gravação

Logar só coloca o erro em uma fila limitada a 256 itens, então nunca espera o banco; com a fila cheia, o que chega é descartado.
Um serviço de segundo plano grava a fila. Se a gravação falhar, sai um aviso no console e aquele registro se perde; o erro
original já foi ao console pelo log que o gerou. O `DbContext` do módulo não usa o log do EF Core, para o banco fora do ar não
gerar outro erro a cada tentativa.

## Catch que escondiam a causa

Falhas de backup, restauração, retenção, limpeza e exportação de anexos, gravação e leitura de anexos e fechamento automático
de faturas passam a registrar a exceção (`OperationLog`), com a mesma mensagem ao usuário. Continuam sem registro os `catch` de
erros esperados, como limite de tamanho no envio de arquivo e conflito de restrição.

No console, esses logs também incluem a mensagem da exceção, que pode citar caminhos de arquivo.

## Backup e restauração

A tabela entra no backup como qualquer schema. Restaurar volta a tabela ao estado do backup, e os erros mais recentes se perdem.
