using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class SharingRuleTests : ReimbursementFixture
{
    private IPersonCatalog People => Provider.GetRequiredService<IPersonCatalog>();
    private ISharingRuleCatalog Rules => Provider.GetRequiredService<ISharingRuleCatalog>();
    private static readonly AccountingMonth s_start = new(2026, 10);

    private async Task<Guid> PersonAsync(string name = "Pessoa Exemplo") => (await People.SaveAsync(null, name, "")).ExternalId!.Value;

    [Fact]
    public async Task PersonDefaultsThePayerToTheNameAndRejectsDuplicateNames()
    {
        var id = await PersonAsync("Maria Exemplo");
        var person = Assert.Single(await People.GetAsync());
        Assert.Equal((id, "Maria Exemplo"), (person.ExternalId, person.PayerDescription));
        Assert.False((await People.SaveAsync(null, "  maria   EXEMPLO ", "")).Succeeded);
        Assert.False((await People.SaveAsync(null, "", "")).Succeeded);
        Assert.True((await People.SaveAsync(id, "Maria Exemplo", "Maria pagadora")).Succeeded);
        Assert.Equal("Maria pagadora", Assert.Single(await People.GetAsync()).PayerDescription);
    }

    [Fact]
    public async Task RuleAppearsCurrentFromItsStartMonthWithThePercent()
    {
        var person = await PersonAsync();
        var filter = await SaveFilterAsync();
        var saved = await Rules.SaveAsync(null, person, filter, 50m, s_start);
        Assert.True(saved.Succeeded, saved.Error);
        var rule = Assert.Single(await Rules.GetAsync(person));
        Assert.Equal((50m, s_start, "Despesas da casa"), (rule.Percent, rule.StartMonth, rule.SavedFilterName));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(33.333)]
    public async Task RuleRejectsPercentOutsideZeroToOneHundredOrBeyondTwoDecimals(double percent)
    {
        var result = await Rules.SaveAsync(null, await PersonAsync(), await SaveFilterAsync(), (decimal)percent, s_start);
        Assert.False(result.Succeeded);
        Assert.Empty(await Rules.GetAsync());
    }

    [Fact]
    public async Task RuleAcceptsTheBoundsAndRejectsDuplicatesOfPersonFilterAndStartMonth()
    {
        var person = await PersonAsync();
        var filter = await SaveFilterAsync();
        Assert.True((await Rules.SaveAsync(null, person, filter, 100m, s_start)).Succeeded);
        Assert.True((await Rules.SaveAsync(null, person, filter, 0m, s_start.AddMonths(1))).Succeeded);
        Assert.False((await Rules.SaveAsync(null, person, filter, 30m, s_start)).Succeeded);
        Assert.False((await Rules.SaveAsync(null, person, Guid.NewGuid(), 30m, s_start)).Succeeded);
        Assert.False((await Rules.SaveAsync(null, Guid.NewGuid(), filter, 30m, s_start)).Succeeded);
        Assert.Equal(2, (await Rules.GetAsync()).Count);
    }

    [Fact]
    public async Task EditingAFilterAfterwardsIsSeenByTheRuleBecauseNothingIsFrozen()
    {
        var person = await PersonAsync();
        var filter = await SaveFilterAsync();
        await Rules.SaveAsync(null, person, filter, 50m, s_start);
        var filters = Provider.GetRequiredService<ISavedFilterCatalog>();
        Assert.True((await filters.SaveAsync(filter, "Despesas da casa", SavedFilterCriteria.Empty with { Text = "internet" })).Succeeded);
        var rule = Assert.Single(await Rules.GetAsync(person));
        Assert.Equal(filter, rule.SavedFilterExternalId);
        Assert.Equal("internet", (await filters.FindAsync(rule.SavedFilterExternalId))!.Criteria.Text);
    }

    [Fact]
    public async Task FilterUsedByARuleCannotBeDeletedAndNamesThePersonUntilTheRuleIsRemoved()
    {
        var person = await PersonAsync("Maria Exemplo");
        var filter = await SaveFilterAsync();
        var rule = await Rules.SaveAsync(null, person, filter, 50m, s_start);
        var filters = Provider.GetRequiredService<ISavedFilterCatalog>();
        var blocked = await filters.DeleteAsync(filter);
        Assert.Contains("Maria Exemplo", blocked.Error);
        Assert.False((await People.DeleteAsync(person)).Succeeded);
        Assert.True((await Rules.DeleteAsync(rule.ExternalId!.Value)).Succeeded);
        Assert.True((await filters.DeleteAsync(filter)).Succeeded);
        Assert.True((await People.DeleteAsync(person)).Succeeded);
    }
}
