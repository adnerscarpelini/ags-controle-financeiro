using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ags.Finance.ArchitectureTests;

public sealed class SharedKernelTests
{
    [Fact]
    public void SharedKernelDoesNotDependOnModulesOrOnTheHost()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Ags\.Finance\.SharedKernel(\..+)?$")
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^Ags\.Finance\.(Modules|Web)(\..+)?$")
            .Because("o SharedKernel é referenciado por todos e não conhece nenhum módulo")
            .WithoutRequiringPositiveResults();

        rule.Check(ArchitectureModel.Architecture);
    }

    [Fact]
    public void SharedKernelProjectReferencesNothing()
    {
        var references = ProjectFiles.ProjectReferencesOf("Ags.Finance.SharedKernel", "src");

        Assert.True(
            references.Count == 0,
            $"O SharedKernel não pode referenciar projetos, mas referencia: {string.Join(", ", references)}");
    }
}
