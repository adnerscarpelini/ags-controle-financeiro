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
    public void OnlyUserCommandsUseTheUserCreationUseCases()
    {
        IArchRule rule = Types().That().ResideInNamespaceMatching(@"^Ags\.Finance\.Web(\..+)?$")
            .And().DoNotResideInNamespaceMatching(@"^Ags\.Finance\.Web\.Commands(\..+)?$")
            .Should().NotDependOnAnyTypesThat().HaveFullNameMatching(@"^Ags\.Finance\.Modules\.Identity\.Contracts\.(ICreateUser|IResetPassword)$")
            .Because("o usuário só é criado ou redefinido pelos comandos no computador do serviço; páginas e endpoints nunca oferecem isso")
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

    [Fact]
    public void SolutionDoesNotDependOnSqlite()
    {
        var root = ProjectFiles.FindRepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.csproj", SearchOption.AllDirectories))
            .Append(Path.Combine(root, "Directory.Packages.props"));

        var offenders = files.Where(file => File.ReadAllText(file).Contains("Sqlite", StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.True(offenders.Count == 0, "PostgreSQL é o único banco suportado, mas há referência a SQLite em: " + string.Join(", ", offenders));
    }
}
