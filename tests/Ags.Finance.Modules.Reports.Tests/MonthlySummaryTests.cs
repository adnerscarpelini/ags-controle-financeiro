using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class MonthlySummaryTests : ReportFixture
{
    private static readonly ReferenceMonth s_october = new(2026, 10);
    private IMonthlySummaries Summaries => Provider.GetRequiredService<IMonthlySummaries>();

    [Fact]
    public async Task IncomeAndExpensesComeOnlyFromTheReferenceMonth()
    {
        await AddAsync(new ReferenceMonth(2026, 9), 500m);
        await AddAsync(s_october, 10000m, income: true);
        await AddAsync(s_october, 8000m);
        await AddAsync(new ReferenceMonth(2026, 11), 9999m);
        var summary = await Summaries.GetAsync(s_october);
        Assert.Equal((new Money(10000m), new Money(8000m)), (summary.Income, summary.Expenses));
    }

    [Fact]
    public async Task TransfersAreOutsideIncomeAndExpenses()
    {
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = (await accounts.GetBanksAsync())[0];
        var second = await accounts.SaveAccountAsync(new(null, bank.ExternalId, "Segunda Conta", AccountKind.Savings, "green", Money.Zero, new DateOnly(2026, 1, 1)));
        Assert.True(second.Succeeded, second.Error);
        var transfer = await Provider.GetRequiredService<IAccountMovementCatalog>().TransferAsync(new(Guid.CreateVersion7(), AccountId, second.ExternalId!.Value, new DateOnly(2026, 10, 6), new Money(300m)));
        Assert.True(transfer.Succeeded, transfer.Error);
        var summary = await Summaries.GetAsync(s_october);
        Assert.Equal((Money.Zero, Money.Zero), (summary.Income, summary.Expenses));
    }

    [Fact]
    public async Task InvoiceAdjustmentsRaiseOrLowerExpensesWithoutCreatingIncome()
    {
        var card = (await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, AccountId, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"))).ExternalId!.Value;
        var purchase = await Entries.CreateAsync(new(new DateOnly(2026, 10, 1), MerchantId, ExpenseSubcategoryId, AccountId, new Money(100m), CardExternalId: card));
        Assert.True(purchase.Succeeded, purchase.Error);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(100.01m)))).Succeeded);
        var raised = await Summaries.GetAsync(s_october);
        Assert.Equal((new Money(100.01m), Money.Zero), (raised.Expenses, raised.Income));
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(99.99m)))).Succeeded);
        var lowered = await Summaries.GetAsync(s_october);
        Assert.Equal((new Money(99.99m), Money.Zero), (lowered.Expenses, lowered.Income));
    }

    [Fact]
    public async Task PendingRecurringOccurrencesCountOnceInTheMonthBudget()
    {
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 5), MerchantId, ExpenseSubcategoryId, AccountId, new Money(40m))))).Succeeded);
        Assert.Equal(new Money(40m), (await Summaries.GetAsync(s_october)).Expenses);
    }
}
