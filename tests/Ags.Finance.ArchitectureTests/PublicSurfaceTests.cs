namespace Ags.Finance.ArchitectureTests;

/// <summary>Em cada módulo, só DependencyInjection e os tipos de Contracts são públicos.</summary>
public sealed class PublicSurfaceTests
{
    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void OnlyDependencyInjectionAndContractsArePublic(string module)
    {
        var moduleNamespace = ArchitectureModel.NamespaceOf(module);
        var contractsNamespace = $"{moduleNamespace}.Contracts";

        var violations = ArchitectureModel.AssemblyOf(module).GetExportedTypes()
            .Where(type => type.FullName != $"{moduleNamespace}.DependencyInjection"
                && type.Namespace != contractsNamespace
                && type.Namespace?.StartsWith($"{contractsNamespace}.", StringComparison.Ordinal) != true)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Módulo {module}: só DependencyInjection e tipos de Contracts podem ser públicos, mas há: {string.Join(", ", violations)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void EveryModuleExposesItsDependencyInjection(string module)
    {
        var type = ArchitectureModel.AssemblyOf(module)
            .GetType($"{ArchitectureModel.NamespaceOf(module)}.DependencyInjection");

        Assert.NotNull(type);
        Assert.True(type.IsPublic && type.IsAbstract && type.IsSealed, "DependencyInjection deve ser uma classe estática pública");
        Assert.NotNull(type.GetMethod($"Add{module}Module"));
    }

    public static IEnumerable<object[]> ModuleNames()
    {
        return ArchitectureModel.ModuleNames();
    }
}
