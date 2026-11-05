using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Reports.Tests;

public sealed class MonthlyBehaviorTests : ReportFixture
{
    private static readonly ReferenceMonth s_september = new(2026, 9);
    private static readonly ReferenceMonth s_october = new(2026, 10);
    private static readonly ReferenceMonth s_november = new(2026, 11);
    private IMonthlyBehaviors Behaviors => Provider.GetRequiredService<IMonthlyBehaviors>();
    private IMonthlySummaries Summaries => Provider.GetRequiredService<IMonthlySummaries>();

    [Fact]
    public async Task ACardPurchaseCountsInThePurchaseMonthWhileTheBudgetKeepsTheInvoiceMonth()
    {
        var card = await CardAsync("Cartão Exemplo");
        await PurchaseAsync(card, new DateOnly(2026, 10, 20), 100m);

        Assert.Equal(new Money(100m), (await Behaviors.GetAsync(s_october)).Spent);
        Assert.Equal(Money.Zero, (await Behaviors.GetAsync(s_november)).Spent);
        Assert.Equal(Money.Zero, (await Summaries.GetAsync(s_october)).Expenses);
    }

    [Fact]
    public async Task AnInstallmentPurchaseCountsWholeInThePurchaseMonth()
    {
        var card = await CardAsync("Cartão Exemplo");
        await PurchaseAsync(card, new DateOnly(2026, 10, 20), 300m, installments: 3);

        Assert.Equal(new Money(300m), (await Behaviors.GetAsync(s_october)).Spent);
        Assert.Equal(new Money(300m), (await Behaviors.GetAsync(s_november)).PreviousMonthSpent);
        Assert.Equal(Money.Zero, (await Behaviors.GetAsync(s_november)).Spent);
        Assert.Equal(Money.Zero, (await Behaviors.GetAsync(new ReferenceMonth(2026, 12))).Spent);
    }

    [Fact]
    public async Task EveryPaymentMethodEntersAndIncomeStaysOut()
    {
        var card = await CardAsync("Cartão Exemplo");
        await PurchaseAsync(card, new DateOnly(2026, 10, 20), 100m);
        await AddAsync(s_october, 50m);
        await AddAsync(s_october, 9000m, income: true);

        Assert.Equal(new Money(150m), (await Behaviors.GetAsync(s_october)).Spent);
    }

    [Fact]
    public async Task ChoosingAnotherInvoiceDoesNotMoveThePurchaseOutOfTheMonthOfItsDate()
    {
        var december = new ReferenceMonth(2026, 12);
        var card = await CardAsync("Cartão Exemplo");
        var entry = await Entries.CreateAsync(new(new DateOnly(2026, 10, 20), MerchantId, ExpenseSubcategoryId, AccountId, new Money(150m), Month: december, CardExternalId: card));
        Assert.True(entry.Succeeded, entry.Error);

        Assert.Equal(new Money(150m), (await Behaviors.GetAsync(s_october)).Spent);
        Assert.Equal(new Money(150m), (await Summaries.GetAsync(december)).Expenses);
    }

    [Fact]
    public async Task YearAverageDividesByMonthsWithSpendingAndIgnoresLaterMonths()
    {
        await AddAsync(new ReferenceMonth(2026, 1), 100m);
        await AddAsync(new ReferenceMonth(2026, 3), 300m);
        await AddAsync(s_september, 500m);
        await AddAsync(s_october, 200m);
        await AddAsync(s_november, 9999m);

        var behavior = await Behaviors.GetAsync(s_october);

        Assert.Equal((new Money(275m), 4, new Money(500m)), (behavior.YearAverageSpent, behavior.MonthsWithSpending, behavior.PreviousMonthSpent));
    }

    [Fact]
    public async Task PreviousMonthOfJanuaryIsDecemberOfTheYearBeforeAndStaysOutOfTheAverage()
    {
        await AddAsync(new ReferenceMonth(2025, 12), 70m);
        await AddAsync(new ReferenceMonth(2026, 1), 10m);

        var behavior = await Behaviors.GetAsync(new ReferenceMonth(2026, 1));

        Assert.Equal((new Money(70m), new Money(10m), 1), (behavior.PreviousMonthSpent, behavior.YearAverageSpent, behavior.MonthsWithSpending));
    }

    [Fact]
    public async Task AMonthWithoutPurchasesIsZero()
    {
        var behavior = await Behaviors.GetAsync(s_october);

        Assert.Equal((Money.Zero, Money.Zero, Money.Zero, 0), (behavior.Spent, behavior.PreviousMonthSpent, behavior.YearAverageSpent, behavior.MonthsWithSpending));
    }

    [Fact]
    public async Task InvoicePaymentsAndTransfersAreNotSpending()
    {
        var card = await CardAsync("Cartão Exemplo");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 100m);
        var payment = await Provider.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), card, s_october, AccountId, new DateOnly(2026, 10, 2), new Money(30m), false));
        Assert.True(payment.Succeeded, payment.Error);
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var bank = (await accounts.GetBanksAsync())[0];
        var second = await accounts.SaveAccountAsync(new(null, bank.ExternalId, "Segunda Conta", AccountKind.Savings, "green", Money.Zero, new DateOnly(2026, 1, 1)));
        Assert.True(second.Succeeded, second.Error);
        var transfer = await Provider.GetRequiredService<IAccountMovementCatalog>().TransferAsync(new(Guid.CreateVersion7(), AccountId, second.ExternalId!.Value, new DateOnly(2026, 10, 2), new Money(300m)));
        Assert.True(transfer.Succeeded, transfer.Error);

        Assert.Equal(new Money(100m), (await Behaviors.GetAsync(s_october)).Spent);
    }

    [Fact]
    public async Task InvoiceAdjustmentsCountOnceInTheirInvoiceMonth()
    {
        var card = await CardAsync("Cartão Exemplo");
        await PurchaseAsync(card, new DateOnly(2026, 10, 1), 100m);
        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(100.01m)))).Succeeded);
        Assert.Equal(new Money(100.01m), (await Behaviors.GetAsync(s_october)).Spent);

        Assert.True((await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), card, s_october, new Money(99.99m)))).Succeeded);
        Assert.Equal(new Money(99.99m), (await Behaviors.GetAsync(s_october)).Spent);
    }

    [Fact]
    public async Task ARecurringOccurrenceDatedInTheMonthIsGeneratedAndCountsOnce()
    {
        Assert.True((await Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 10, 5), MerchantId, ExpenseSubcategoryId, AccountId, new Money(40m))))).Succeeded);

        Assert.Equal(new Money(40m), (await Behaviors.GetAsync(s_october)).Spent);
        Assert.Equal(new Money(40m), (await Behaviors.GetAsync(s_october)).Spent);
    }
}
