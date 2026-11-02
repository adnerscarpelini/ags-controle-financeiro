using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ags.Finance.ArchitectureTests;

/// <summary>Um módulo só usa outro pelos Contracts, e só nas direções da tabela de module-structure.md.</summary>
public sealed class ModuleDependencyTests
{
    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleUsesOtherModulesOnlyThroughContracts(string module)
    {
        var anotherModuleOutsideContracts =
            $@"^Ags\.Finance\.Modules\.(?!{module}(\.|$))\w+(\.(?!Contracts(\.|$)).+)?$";

        IArchRule rule = Types().That().ResideInNamespaceMatching(ModuleNamespace(module))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(anotherModuleOutsideContracts)
            .Because("um módulo só enxerga os Contracts dos outros")
            .WithoutRequiringPositiveResults();

        rule.Check(ArchitectureModel.Architecture);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleDependsOnlyOnModulesAllowedByTheDocumentation(string module)
    {
        var forbidden = ArchitectureModel.AllowedDependencies.Keys
            .Where(other => other != module && !ArchitectureModel.AllowedDependencies[module].Contains(other))
            .ToList();
        var forbiddenModules = $@"^Ags\.Finance\.Modules\.({string.Join("|", forbidden)})(\..+)?$";

        IArchRule rule = Types().That().ResideInNamespaceMatching(ModuleNamespace(module))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(forbiddenModules)
            .Because("as dependências entre módulos seguem docs/architecture/module-structure.md")
            .WithoutRequiringPositiveResults();

        rule.Check(ArchitectureModel.Architecture);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleProjectReferencesMatchTheDocumentation(string module)
    {
        var allowed = ArchitectureModel.AllowedDependencies[module]
            .Select(other => $"Ags.Finance.Modules.{other}")
            .Append("Ags.Finance.SharedKernel")
            .ToHashSet();

        var references = ProjectFiles.ProjectReferencesOf($"Ags.Finance.Modules.{module}", "src/Modules");

        var notAllowed = references.Where(reference => !allowed.Contains(reference)).ToList();
        Assert.True(
            notAllowed.Count == 0,
            $"Módulo {module} referencia projetos fora da tabela de module-structure.md: {string.Join(", ", notAllowed)}");
    }

    [Fact]
    public void DependencyTableHasNoCycles()
    {
        var table = ArchitectureModel.AllowedDependencies;
        var done = new HashSet<string>();

        foreach (var module in table.Keys)
        {
            Visit(module, [], done, table);
        }
    }

    [Fact]
    public void DependencyTableOnlyMentionsKnownModules()
    {
        var table = ArchitectureModel.AllowedDependencies;
        var unknown = table.Values.SelectMany(dependencies => dependencies).Where(name => !table.ContainsKey(name)).ToList();

        Assert.Empty(unknown);
    }

    public static IEnumerable<object[]> ModuleNames()
    {
        return ArchitectureModel.ModuleNames();
    }

    private static string ModuleNamespace(string module)
    {
        return $@"^Ags\.Finance\.Modules\.{module}(\..+)?$";
    }

    private static void Visit(
        string module,
        List<string> path,
        HashSet<string> done,
        IReadOnlyDictionary<string, string[]> table)
    {
        if (done.Contains(module))
        {
            return;
        }

        Assert.False(path.Contains(module), $"Ciclo entre módulos: {string.Join(" -> ", path.Append(module))}");

        path.Add(module);
        foreach (var dependency in table[module])
        {
            Visit(dependency, path, done, table);
        }

        path.RemoveAt(path.Count - 1);
        done.Add(module);
    }
}
