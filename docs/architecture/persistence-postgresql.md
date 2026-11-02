# Persistência com PostgreSQL

## Estado e contexto

**Decisão aprovada, implementação pendente na [#60](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/60).**
O código atual ainda usa SQLite, documentado como [legado em transição](persistence-sqlite.md).
A distribuição Docker é entregue em seguida na [#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59).
Esta nota registra a decisão; não é um guia de execução de uma migração já disponível.

O projeto ainda não está em uso real. O usuário autorizou recriar dados e migrations de
desenvolvimento para simplificar a mudança. Não haverá conversão de dados SQLite nem dois
providers mantidos em paralelo.

## Decisão

- PostgreSQL é o único banco de destino, em serviço separado da aplicação no Compose.
- Um banco, com schema, DbContext e histórico de migrations próprios por módulo persistente.
  A #60 define e valida seus nomes e os mapeamentos do provider.
- Money continua decimal no domínio e centavos em BIGINT; AccountingMonth continua inteiro yyyyMM.
  ExternalId continua Guid v7 gerado no domínio, único; auditoria continua UTC via TimeProvider.
- Sem FK, JOIN, SQL ou acesso a tabelas que cruze módulos. Schemas não substituem a fronteira
  dos Contracts. A [tabela de dependências](module-structure.md) continua sem ciclos.
- PostgreSQL usa volume local próprio. DataDirectory fica para certificados, Data Protection
  e arquivos da aplicação, incluindo logos quando implementados. Dados ativos não ficam em pasta sincronizada.
- Npgsql, migrations, consultas, tratamento de conflitos e testes relacionais serão adaptados
  na #60. Integração usa PostgreSQL real isolado; regras de domínio continuam independentes do provider.
- Pacotes, scripts e testes exclusivos de SQLite serão retirados com sua substituição.
  A limpeza é restrita aos alvos conferidos deste projeto, sem apagar outros bancos/volumes ou o histórico Git.

## Recuperação e atualização

A [#59](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/59) inclui backup sob demanda
e ensaio de recuperação; a [#31](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/31)
adiciona interface, agenda e retenção.

Usar pg_dump/pg_restore com versões compatíveis. O procedimento precisa preparar papéis e
permissões, preservar arquivos associados e testar uma instalação vazia, conferindo login,
identidades, vínculos e totais. Uma cópia concluída precisa existir fora da máquina/disco original.

Volumes persistentes protegem a recriação de containers, mas não garantem recuperação da perda
do host. CSV é troca de dados do sistema e não substitui esse backup. A recuperação lógica
retorna ao estado do backup concluído; recuperação por instante/WAL não está aprovada neste escopo.

O reset autorizado vale para esta transição de desenvolvimento. Atualizações posteriores
preservam os dados e exigem backup antes de migrations; retornar à imagem anterior não desfaz
as mudanças do banco.

## Alternativas e consequências

- SQLite foi escolhido inicialmente pela simplicidade; seu fluxo passa a ser histórico.
- Manter dois providers exigiria duplicar migrations e testes, sem necessidade aprovada.
- PostgreSQL adiciona serviço, credenciais e preparação de recuperação, com instalação
  padronizada pelo Compose e um processo de restauração verificável.
- Operações financeiras que atravessam módulos ainda exigem desenho explícito de atomicidade
  e concorrência. A escolha do banco não autoriza acesso cruzado ou sucesso parcial silencioso.
