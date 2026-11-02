using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ags.Finance.ArchitectureTests;

public sealed class HostTests
{
    [Fact]
    public void BlazorComponentsDoNotUseModuleInfrastructure()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Ags\.Finance\.Web\.Components(\..+)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Ags\.Finance\.Modules\.\w+\.Infrastructure(\..+)?$")
            .Because("páginas e componentes chamam casos de uso pelos Contracts, nunca a Infrastructure")
            .WithoutRequiringPositiveResults();

        rule.Check(ArchitectureModel.Architecture);
    }

    [Fact]
    public void HostComponentsAreLoadedInTheArchitectureModel()
    {
        var componentTypes = ArchitectureModel.Architecture.Types
            .Where(type => type.Namespace.FullName.StartsWith("Ags.Finance.Web.Components", StringComparison.Ordinal));

        Assert.NotEmpty(componentTypes);
    }
}
