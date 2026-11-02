# Categorias e subcategorias

A [#9](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/9) entrega
Cadastros > Categorias e subcategorias. Categorias separam Despesa, Receita e
Transferência; subcategorias aparecem como `Categoria > Subcategoria`.

Nomes de subcategorias são únicos dentro da categoria, ignorando caixa e espaços
nas extremidades. A restrição também é garantida pelo PostgreSQL. Os casos de uso
validam a categoria ativa; consultas de seleção ocultam subcategorias cujo pai está
desativado. Desativar preserva identidade e histórico, com confirmação na interface.

O módulo Transactions publica DTOs e `IClassificationCatalog`; componentes não
acessam o DbContext. Cada operação usa um contexto curto criado pela factory, para
evitar compartilhar um contexto entre ações de um circuito Blazor.
