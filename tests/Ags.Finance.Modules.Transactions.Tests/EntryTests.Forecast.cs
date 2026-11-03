using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task ForecastAddsOnlyThePendingEntriesOfTheMonthToTheCurrentBalance()
    {
        var date = new DateOnly(2026, 11, 5);
        Assert.True((await Entries.CreateAsync(Request() with { Date = date, Amount = new Money(100), IsPaid = true })).Succeeded);
        Assert.True((await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 20), Amount = new Money(30), IsPaid = false })).Succeeded);
        Assert.True((await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 12, 20), Amount = new Money(40), IsPaid = false })).Succeeded);

        var forecast = await Entries.GetBalanceForecastAsync(_accountId, date, new ReferenceMonth(2026, 11));

        Assert.NotNull(forecast);
        Assert.Equal(new Money(900), forecast.CurrentBalance);
        Assert.Equal(new Money(-30), forecast.PendingBalance);
        Assert.Equal(new Money(870), forecast.ForecastBalance);
    }

    [Fact]
    public async Task WithoutAccountTheForecastSumsTheActiveAccountsAndIgnoresTheArchivedOnes()
    {
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var existing = Assert.Single(await accounts.GetAccountsAsync());
        var other = await accounts.SaveAccountAsync(new(null, existing.BankExternalId, "Outra Conta", AccountKind.Checking, "green", new Money(500), new DateOnly(2026, 1, 1)));
        var archived = await accounts.SaveAccountAsync(new(null, existing.BankExternalId, "Conta Antiga", AccountKind.Checking, "green", new Money(9000), new DateOnly(2026, 1, 1)));
        await accounts.DeactivateAccountAsync(archived.ExternalId!.Value);

        var forecast = await Entries.GetBalanceForecastAsync(null, new DateOnly(2026, 11, 5), new ReferenceMonth(2026, 11));

        Assert.True(other.Succeeded);
        Assert.Equal(new Money(1500), forecast!.CurrentBalance);
    }

    [Fact]
    public async Task AForecastForAnUnknownAccountIsNull()
    {
        Assert.Null(await Entries.GetBalanceForecastAsync(Guid.NewGuid(), new DateOnly(2026, 11, 5), new ReferenceMonth(2026, 11)));
    }

    [Theory]
    [InlineData(EntrySortColumn.Amount, false, -300)]
    [InlineData(EntrySortColumn.Amount, true, -100)]
    [InlineData(EntrySortColumn.Date, false, -200)]
    [InlineData(EntrySortColumn.Date, true, -100)]
    public async Task TheQueryOrdersByTheChosenColumnAndDirection(EntrySortColumn column, bool descending, int firstAmount)
    {
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 1), Amount = new Money(200) });
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 2), Amount = new Money(300) });
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 3), Amount = new Money(100) });

        var result = await Entries.QueryAsync(new(Month: new ReferenceMonth(2026, 11), SortColumn: column, SortDescending: descending));

        Assert.Equal(new Money(firstAmount), result.Entries[0].Amount);
    }

    [Fact]
    public async Task TheQueryOrdersByDescriptionAndCategoryAcrossPages()
    {
        var classifications = Provider.GetRequiredService<IClassificationCatalog>();
        var merchants = Provider.GetRequiredService<IMerchantCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "AAA Primeira", CategoryKind.Expense);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Exemplo");
        var merchant = await merchants.SaveAsync(null, "ZZZ Última", subcategory.ExternalId);
        await Entries.CreateAsync(Request());
        await Entries.CreateAsync(Request() with { MerchantExternalId = merchant.ExternalId!.Value, SubcategoryExternalId = subcategory.ExternalId!.Value });
        var month = Request().Date;
        var byDescription = await Entries.QueryAsync(new(Month: ReferenceMonth.FromDate(month), SortColumn: EntrySortColumn.Description, SortDescending: true, PageSize: 1));
        var byCategory = await Entries.QueryAsync(new(Month: ReferenceMonth.FromDate(month), SortColumn: EntrySortColumn.Category, SortDescending: false, PageSize: 1));

        Assert.Equal("ZZZ Última", Assert.Single(byDescription.Entries).MerchantName);
        Assert.Equal(2, byDescription.TotalCount);
        Assert.Equal("ZZZ Última", Assert.Single(byCategory.Entries).MerchantName);
    }

    [Fact]
    public async Task ASavedFilterKeepsTheChosenOrder()
    {
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 1), Amount = new Money(200) });
        await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 11, 2), Amount = new Money(300) });
        var filters = Provider.GetRequiredService<ISavedFilterCatalog>();
        var saved = await filters.SaveAsync(null, "Filtro Exemplo", SavedFilterCriteria.Empty with { AccountExternalIds = [_accountId] });
        Assert.True(saved.Succeeded, saved.Error);

        var result = await filters.RunAsync(saved.ExternalId!.Value, new ReferenceMonth(2026, 11), sortColumn: EntrySortColumn.Amount, sortDescending: false);

        Assert.Equal(new Money(-300), result.Entries[0].Amount);
    }
}
