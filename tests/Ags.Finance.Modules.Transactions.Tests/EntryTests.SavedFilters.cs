using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using CategoryKind = Ags.Finance.Modules.Transactions.Contracts.CategoryKind;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private ISavedFilterCatalog SavedFilters => Provider.GetRequiredService<ISavedFilterCatalog>();

    private async Task<Guid> SubcategoryAsync(string name)
    {
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var category = (await classifications.GetCategoriesAsync()).Single();
        return (await classifications.SaveSubcategoryAsync(null, category.ExternalId, name)).ExternalId!.Value;
    }

    private static SavedFilterCriteria Subcategories(params Guid[] ids) => SavedFilterCriteria.Empty with { SubcategoryExternalIds = ids };

    [Fact]
    public async Task SavedFilterRunsOnlyItsSubcategoriesInTheChosenMonthAndFollowsTheMonth()
    {
        var energy = await SubcategoryAsync("Energia");
        var water = await SubcategoryAsync("Água");
        await Entries.CreateAsync(Request() with { SubcategoryExternalId = energy });
        await Entries.CreateAsync(Request() with { SubcategoryExternalId = water, Date = new DateOnly(2026, 11, 5), Month = new AccountingMonth(2026, 11) });
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 6), Month = new AccountingMonth(2026, 11) });
        var saved = await SavedFilters.SaveAsync(null, "Despesas da casa", Subcategories(energy, water));
        Assert.True(saved.Succeeded, saved.Error);

        var october = await SavedFilters.RunAsync(saved.ExternalId!.Value, new AccountingMonth(2026, 10));
        Assert.Equal(energy, Assert.Single(october.Entries).SubcategoryExternalId);
        var november = await SavedFilters.RunAsync(saved.ExternalId!.Value, new AccountingMonth(2026, 11));
        Assert.Equal(water, Assert.Single(november.Entries).SubcategoryExternalId);
        Assert.Equal(new Money(-150), november.Balance);
    }

    [Fact]
    public async Task SavedFilterRejectsDuplicateNamesAndEmptyCriteriaAndAllowsRenaming()
    {
        var first = await SavedFilters.SaveAsync(null, "Despesas da casa", Subcategories(_subcategoryId));
        Assert.False((await SavedFilters.SaveAsync(null, "  despesas  DA casa ", Subcategories(_subcategoryId))).Succeeded);
        Assert.False((await SavedFilters.SaveAsync(null, "Vazio", SavedFilterCriteria.Empty)).Succeeded);
        var renamed = await SavedFilters.SaveAsync(first.ExternalId, "Casa", Subcategories(_subcategoryId));
        Assert.True(renamed.Succeeded, renamed.Error);
        Assert.Equal("Casa", Assert.Single(await SavedFilters.GetAsync()).Name);
    }

    [Fact]
    public async Task SavedFilterKeepsWorkingAfterItsSubcategoryIsDeactivated()
    {
        var energy = await SubcategoryAsync("Energia");
        await Entries.CreateAsync(Request() with { SubcategoryExternalId = energy });
        var saved = await SavedFilters.SaveAsync(null, "Energia", Subcategories(energy));
        await Provider.GetRequiredService<IClassificationCatalog>().DeactivateAsync(energy, true);
        Assert.Single((await SavedFilters.RunAsync(saved.ExternalId!.Value, new AccountingMonth(2026, 10))).Entries);
    }

    [Fact]
    public async Task SavedPendingFilterDropsAnOccurrenceOnceItIsEffectivated()
    {
        Assert.True((await Entries.CreateRecurringAsync(new(Request()))).Succeeded);
        var saved = await SavedFilters.SaveAsync(null, "Pendentes", SavedFilterCriteria.Empty with { AccountExternalIds = [_accountId], IsEffective = false });
        var month = new AccountingMonth(2026, 10);
        var occurrence = Assert.Single((await SavedFilters.RunAsync(saved.ExternalId!.Value, month)).Entries);
        Assert.True((await Entries.ConfirmEffectiveAsync(occurrence.ExternalId, occurrence.Date)).Succeeded);
        Assert.Empty((await SavedFilters.RunAsync(saved.ExternalId!.Value, month)).Entries);
    }

    [Fact]
    public async Task SavedFilterCanBeDeleted()
    {
        var saved = await SavedFilters.SaveAsync(null, "Casa", Subcategories(_subcategoryId));
        Assert.True((await SavedFilters.DeleteAsync(saved.ExternalId!.Value)).Succeeded);
        Assert.Empty(await SavedFilters.GetAsync());
    }
}
