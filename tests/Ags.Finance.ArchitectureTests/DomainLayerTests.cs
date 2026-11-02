using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ags.Finance.ArchitectureTests;

/// <summary>O Domain de cada módulo só depende da BCL, do SharedKernel e de si mesmo.</summary>
public sealed class DomainLayerTests
{
    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void DomainDoesNotDependOnFrameworks(string module)
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(DomainNamespace(module))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Microsoft\.(EntityFrameworkCore|AspNetCore)(\..+)?$")
            .Because("o Domain depende só da BCL e do SharedKernel (EF Core, ASP.NET Core e Blazor ficam fora)")
            .WithoutRequiringPositiveResults();

        rule.Check(ArchitectureModel.Architecture);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void DomainDoesNotDependOnOtherLayersOfTheModule(string module)
    {
        var moduleNamespace = ArchitectureModel.NamespaceOf(module).Replace(".", @"\.", StringComparison.Ordinal);
        var otherLayers = $@"^{moduleNamespace}\.(Application|Infrastructure|Contracts)(\..+)?$";

        IArchRule rule = Types().That().ResideInNamespaceMatching(DomainNamespace(module))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(otherLayers)
            .Because("o Domain não depende de Application, Infrastructure nem Contracts")
            .WithoutRequiringPositiveResults();

        rule.Check(ArchitectureModel.Architecture);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void DomainDoesNotDependOnOtherModules(string module)
    {
        var otherModules = $@"^Ags\.Finance\.Modules\.(?!{module}(\.|$))\w+(\..+)?$";

        IArchRule rule = Types().That().ResideInNamespaceMatching(DomainNamespace(module))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(otherModules)
            .Because("o Domain não conhece outros módulos")
            .WithoutRequiringPositiveResults();

        rule.Check(ArchitectureModel.Architecture);
    }

    public static IEnumerable<object[]> ModuleNames()
    {
        return ArchitectureModel.ModuleNames();
    }

    private static string DomainNamespace(string module)
    {
        var moduleNamespace = ArchitectureModel.NamespaceOf(module).Replace(".", @"\.", StringComparison.Ordinal);
        return $@"^{moduleNamespace}\.Domain(\..+)?$";
    }
}
