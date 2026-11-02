# Testes de arquitetura

Implementa a issue [#2](https://github.com/adnerscarpelini/ags-controle-financeiro/issues/2).

## Contexto

As camadas (`Domain`, `Application`, `Infrastructure`, `Contracts`) são pastas dentro de cada class library, então o compilador não impede que uma camada use outra. As regras de fronteira de [module-structure.md](module-structure.md) são verificadas por `dotnet test` e no CI.

## Decisão

O projeto `tests/Ags.Finance.ArchitectureTests` usa a biblioteca **ArchUnitNET** (`TngTech.ArchUnitNET.xUnit`) e roda junto com os demais testes, em Windows e Linux. Regras verificadas:

| Regra | Teste |
| --- | --- |
| `Domain` não depende de EF Core, ASP.NET Core/Blazor, nem de `Application`, `Infrastructure` e `Contracts` do próprio módulo, nem de outros módulos | `DomainLayerTests` |
| Só `DependencyInjection` e os tipos de `Contracts` são públicos; todo módulo expõe `Add<Module>Module` | `PublicSurfaceTests` |
| Um módulo só usa tipos de outro módulo que estejam em `Contracts` | `ModuleDependencyTests` |
| Um módulo só usa e referencia (`ProjectReference`) os módulos permitidos pela tabela; a tabela não tem ciclos | `ModuleDependencyTests` |
| O `SharedKernel` não referencia módulos nem o host | `SharedKernelTests` |
| Componentes Blazor do host não usam tipos da `Infrastructure` dos módulos | `HostTests` |

A tabela de dependências permitidas fica em `ArchitectureModel.AllowedDependencies` e espelha `module-structure.md`. Uma dependência nova entre módulos precisa ser registrada nos dois lugares; sem isso, o teste falha.

As regras usam `WithoutRequiringPositiveResults`: enquanto um módulo não tem tipos em `Domain`, a regra passa sem avaliar nada, e passa a valer assim que o primeiro tipo for criado.

## Alternativas

- **NetArchTest.Rules:** na avaliação original, a versão considerada foi 1.3.2, de 2021; a equipe preferiu as mensagens e regras do ArchUnitNET. Esse registro é histórico, sem afirmar qual é a versão atual da alternativa.
- **Reflexão pura:** serve para visibilidade de tipos (usada em `PublicSurfaceTests`), mas não enxerga dependências dentro do corpo dos métodos.

## Consequências

- Violações aparecem como falha de teste com o tipo e a dependência proibida, por exemplo `Accounts.Domain.X does depend on "Microsoft.AspNetCore.Http.HttpContext"`.
- Os tipos `internal` já impedem, pelo compilador, que um módulo use as camadas internas de outro; os testes cobrem o que o compilador não cobre (visibilidade acidental, `ProjectReference` fora da tabela, camadas do mesmo módulo).
- A regra "o `SharedKernel` não referencia módulos" é verificada também no `.csproj`, porque uma referência dessas criaria ciclo de projetos antes mesmo de existir um tipo que a use.
