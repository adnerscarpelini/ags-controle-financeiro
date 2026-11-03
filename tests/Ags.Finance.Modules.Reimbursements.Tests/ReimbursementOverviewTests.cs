using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reimbursements.Tests;

public sealed class ReimbursementOverviewTests : ReimbursementFixture
{
    private static readonly AccountingMonth s_september = new(2026, 9);
    private static readonly AccountingMonth s_october = new(2026, 10);

    private IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();
    private IReimbursementOverviews Overviews => Provider.GetRequiredService<IReimbursementOverviews>();

    private async Task<Guid> PersonAsync(string name = "Pessoa Exemplo")
        => (await Provider.GetRequiredService<IPersonCatalog>().SaveAsync(null, name, 50m)).ExternalId!.Value;

    private async Task<Guid> ExpenseAsync(decimal amount, Guid person, decimal percent = 50m, DateOnly? date = null)
    {
        var result = await Entries.CreateAsync(new(date ?? new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(amount), Reimbursement: new(person, percent)));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    [Fact]
    public async Task ExpenseWithPersonIsAPendingItemWithTheExpectedAmount()
    {
        var person = await PersonAsync();
        var entry = await ExpenseAsync(142.90m, person);

        var overview = await Overviews.GetAsync(new(s_october));

        var item = Assert.Single(overview.Items);
        Assert.Equal((entry, person, "Pessoa Exemplo", new Money(142.90m), 50m, new Money(71.45m), (Money?)null, ReimbursementStatus.Pending),
            (item.EntryExternalId, item.PersonExternalId, item.PersonName, item.ExpenseAmount, item.Percent, item.Expected, item.Received, item.Status));
        Assert.Equal((new Money(71.45m), Money.Zero, new Money(71.45m)), (overview.Expected, overview.Received, overview.Pending));
    }

    [Fact]
    public async Task ExpectedIsRoundedToTheCent()
        => Assert.Equal(new Money(0.03m), Assert.Single((await AfterAsync(0.05m, 50m)).Items).Expected);

    private async Task<ReimbursementOverview> AfterAsync(decimal amount, decimal percent)
    {
        await ExpenseAsync(amount, await PersonAsync(), percent);
        return await Overviews.GetAsync(new(s_october));
    }

    [Fact]
    public async Task ExpenseWithoutPersonOrIncomeIsNotAnItem()
    {
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(100)))).Succeeded);
        Assert.Empty((await Overviews.GetAsync(new(s_october))).Items);
    }

    [Fact]
    public async Task ZeroPercentIsNotListed()
    {
        var overview = await AfterAsync(100m, 0m);
        Assert.Empty(overview.Items);
        Assert.Equal(Money.Zero, overview.Pending);
    }

    [Fact]
    public async Task CardPurchaseAppearsInItsAccountingMonth()
    {
        var person = await PersonAsync();
        var card = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, AccountId, "Cartão Exemplo", CardBrand.Mastercard, new Money(10000), 3, 10, true, "green"));
        Assert.True((await Entries.CreateAsync(new(new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(100), CardExternalId: card.ExternalId, Reimbursement: new(person, 50m)))).Succeeded);

        Assert.Empty((await Overviews.GetAsync(new(s_october))).Items);
        Assert.Single((await Overviews.GetAsync(new(new AccountingMonth(2026, 11)))).Items);
    }

    [Fact]
    public async Task EachInstallmentIsAnItemOfItsMonthWithTheCentDifferenceOnTheFirst()
    {
        var person = await PersonAsync();
        var card = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, AccountId, "Cartão Exemplo", CardBrand.Mastercard, new Money(10000), 3, 10, true, "green"));
        Assert.True((await Entries.CreateInstallmentsAsync(new(new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(100), Month: s_october, CardExternalId: card.ExternalId, Reimbursement: new(person, 100m)), 3)).Succeeded);

        Assert.Equal(new Money(33.34m), Assert.Single((await Overviews.GetAsync(new(s_october))).Items).Expected);
        Assert.Equal(new Money(33.33m), Assert.Single((await Overviews.GetAsync(new(new AccountingMonth(2026, 11)))).Items).Expected);
        Assert.Equal(new Money(33.33m), Assert.Single((await Overviews.GetAsync(new(new AccountingMonth(2026, 12)))).Items).Expected);
    }

    [Fact]
    public async Task RecurringOccurrenceOfAFutureMonthIsGeneratedAndMarkedAsPendingExpense()
    {
        var person = await PersonAsync();
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 5), MerchantId, SubcategoryId, AccountId, new Money(100), Reimbursement: new(person, 50m))))).Succeeded);

        var item = Assert.Single((await Overviews.GetAsync(new(new AccountingMonth(2027, 3)))).Items);

        Assert.Equal((new Money(50m), false), (item.Expected, item.IsExpenseEffective));
    }

    [Fact]
    public async Task PendingItemFollowsTheExpense()
    {
        var entry = await ExpenseAsync(100m, await PersonAsync());
        var details = Assert.Single(await Entries.GetAsync());

        Assert.True((await Entries.EditAsync(entry, new(details.Date, details.MerchantExternalId, details.SubcategoryExternalId, details.AccountExternalId, new Money(200), details.Details, details.Month, Reimbursement: details.Reimbursement), EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal(new Money(100m), Assert.Single((await Overviews.GetAsync(new(s_october))).Items).Expected);

        Assert.True((await Entries.DeleteAsync(entry, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Empty((await Overviews.GetAsync(new(s_october))).Items);
    }

    [Fact]
    public async Task FilteringByPersonLimitsItemsAndTotals()
    {
        var first = await PersonAsync("Ana Exemplo");
        var second = await PersonAsync("Bia Exemplo");
        await ExpenseAsync(100m, first);
        await ExpenseAsync(200m, second);

        var everyone = await Overviews.GetAsync(new(s_october));
        Assert.Equal(new Money(150m), everyone.Expected);
        Assert.Equal(["Ana Exemplo", "Bia Exemplo"], everyone.People.Select(x => x.PersonName));
        Assert.Equal([new Money(50m), new Money(100m)], everyone.People.Select(x => x.Pending));

        var filtered = await Overviews.GetAsync(new(s_october, second));
        Assert.Equal(second, Assert.Single(filtered.Items).PersonExternalId);
        Assert.Equal(new Money(100m), filtered.Expected);
    }

    [Fact]
    public async Task PreviousPendingsAreWarnedAndAllMonthsListsThem()
    {
        var person = await PersonAsync();
        await ExpenseAsync(100m, person, date: new DateOnly(2026, 9, 5));
        await ExpenseAsync(200m, person);

        var october = await Overviews.GetAsync(new(s_october));
        Assert.Equal(new Money(50m), october.PendingBeforeMonth);
        Assert.Equal(Money.Zero, (await Overviews.GetAsync(new(s_september))).PendingBeforeMonth);

        var all = await Overviews.GetAsync(new(Status: ReimbursementStatus.Pending));
        Assert.Equal(2, all.Items.Count);
        Assert.Equal(new Money(150m), all.Pending);
    }

    [Fact]
    public async Task MonthWithoutItemsIsEmpty()
    {
        var overview = await Overviews.GetAsync(new(s_october));
        Assert.Null(overview.Error);
        Assert.Empty(overview.Items);
    }
}
