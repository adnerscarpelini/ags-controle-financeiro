using Ags.Finance.Modules.Reimbursements.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class PersonTests : ReimbursementFixture
{
    private IPersonCatalog People => Provider.GetRequiredService<IPersonCatalog>();

    [Fact]
    public async Task PersonIsSavedWithDefaultPercentAndCanBeEdited()
    {
        var id = (await People.SaveAsync(null, "Pessoa Exemplo", 50m)).ExternalId!.Value;
        Assert.Equal(new PersonDetails(id, "Pessoa Exemplo", 50m), Assert.Single(await People.GetAsync()));

        Assert.True((await People.SaveAsync(id, "Pessoa Exemplo", 33.33m)).Succeeded);
        Assert.Equal(33.33m, Assert.Single(await People.GetAsync()).DefaultPercent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task LimitPercentsAreAccepted(decimal percent)
        => Assert.True((await People.SaveAsync(null, "Pessoa Exemplo", percent)).Succeeded);

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(33.333)]
    public async Task InvalidPercentIsRejected(decimal percent)
    {
        Assert.False((await People.SaveAsync(null, "Pessoa Exemplo", percent)).Succeeded);
        Assert.Empty(await People.GetAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public async Task InvalidNameIsRejected(string name)
        => Assert.False((await People.SaveAsync(null, name, 50m)).Succeeded);

    [Fact]
    public async Task NameVaryingOnlyByCaseOrAccentIsRejected()
    {
        Assert.True((await People.SaveAsync(null, "José Exemplo", 50m)).Succeeded);
        Assert.Contains("já existe", (await People.SaveAsync(null, "JOSE exemplo", 10m)).Error);
    }

    [Fact]
    public async Task UnusedPersonCanBeDeleted()
    {
        var id = (await People.SaveAsync(null, "Pessoa Exemplo", 50m)).ExternalId!.Value;
        Assert.True((await People.DeleteAsync(id)).Succeeded);
        Assert.Empty(await People.GetAsync());
    }
}
