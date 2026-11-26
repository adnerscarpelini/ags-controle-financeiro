using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private static readonly ReferenceMonth s_november = new(2026, 11);

    private ICashbackCatalog Cashback => Provider.GetRequiredService<ICashbackCatalog>();
    private IEntryCatalog Entries => Provider.GetRequiredService<IEntryCatalog>();

    private async Task SetCardCashbackAsync(decimal? percent, Guid? card = null, string name = "Cartão Exemplo")
    {
        var saved = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(card ?? _cardId, _accountId, name, CardBrand.Visa, new Money(10000), 3, 10, true, "green",
            Cashback: new CardCashback(percent)));
        Assert.True(saved.Succeeded, saved.Error);
    }

    private async Task<EntryDetails> EntryAsync(Guid externalId) => (await Entries.GetAsync()).Single(x => x.ExternalId == externalId);

    private async Task PayOctoberAsync()
    {
        await Lifecycle.CloseAsync(_cardId, s_october);
        var remaining = Assert.Single(await Catalog.GetMonthAsync(s_october)).RemainingBalance;
        var paid = await Payments.ConfirmPaymentAsync(Payment(remaining.Amount, settle: true));
        Assert.True(paid.Succeeded, paid.Error);
        Assert.Equal(InvoiceStatus.Paid, Assert.Single(await Catalog.GetMonthAsync(s_october)).Status);
    }

    private ReceiveCashbackRequest Receive(decimal received, CashbackDestination destination = CashbackDestination.Account, ReferenceMonth? target = null)
        => new(Guid.CreateVersion7(), _cardId, s_october, new DateOnly(2026, 10, 15), new Money(received), destination,
            destination == CashbackDestination.Account ? _accountId : null, target);

    [Fact]
    public async Task PurchaseRecordsTheCardPercentAtCreationAndKeepsItWhenTheCardChanges()
    {
        await SetCardCashbackAsync(1m);
        var first = (await Entries.CreateAsync(Purchase(150))).ExternalId!.Value;
        Assert.Equal(1m, (await EntryAsync(first)).CashbackPercent);
        Assert.Equal(new Money(1.50m), (await EntryAsync(first)).ExpectedCashback);

        await SetCardCashbackAsync(2m);
        var second = (await Entries.CreateAsync(Purchase(100))).ExternalId!.Value;
        Assert.Equal(2m, (await EntryAsync(second)).CashbackPercent);

        var edited = await Entries.EditAsync(first, Purchase(200) with { Details = "Editada" }, EntryChangeScope.OnlyThis);
        Assert.True(edited.Succeeded, edited.Error);
        Assert.Equal(1m, (await EntryAsync(first)).CashbackPercent);
        Assert.Equal(new Money(2m), (await EntryAsync(first)).ExpectedCashback);
        Assert.Equal(new Money(4m), Assert.Single(await Catalog.GetMonthAsync(s_october)).ExpectedCashback);
    }

    [Fact]
    public async Task MovingThePurchaseToAnotherCardRecordsThatCardsPercent()
    {
        await SetCardCashbackAsync(1m);
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var other = (await accounts.SaveCreditCardAsync(new(null, _accountId, "Cartão Dois", CardBrand.Elo, new Money(5000), 3, 10, true, "green", Cashback: new CardCashback(3m)))).ExternalId!.Value;
        var entry = (await Entries.CreateAsync(Purchase(100))).ExternalId!.Value;

        var moved = await Entries.EditAsync(entry, Purchase(100) with { CardExternalId = other }, EntryChangeScope.OnlyThis);

        Assert.True(moved.Succeeded, moved.Error);
        Assert.Equal(3m, (await EntryAsync(entry)).CashbackPercent);
        Assert.Equal(new Money(3m), (await EntryAsync(entry)).ExpectedCashback);
    }

    [Fact]
    public async Task InstallmentsRecordThePercentOnEachPartAndRoundExpectedInCents()
    {
        await SetCardCashbackAsync(1m);
        Assert.True((await Entries.CreateInstallmentsAsync(Purchase(100), 3)).Succeeded);
        var parts = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal([1m, 1m, 1m], parts.Select(x => x.CashbackPercent));
        Assert.Equal([new Money(-33.34m), new Money(-33.33m), new Money(-33.33m)], parts.Select(x => x.Amount));
        Assert.Equal([new Money(0.33m), new Money(0.33m), new Money(0.33m)], parts.Select(x => x.ExpectedCashback));
        Assert.Equal([s_october, s_november, new ReferenceMonth(2026, 12)], parts.Select(x => x.Month));

        await SetCardCashbackAsync(1.5m);
        var rounded = (await Entries.CreateAsync(Purchase(33.33m))).ExternalId!.Value;
        Assert.Equal(new Money(0.50m), (await EntryAsync(rounded)).ExpectedCashback);
    }

    [Fact]
    public async Task RecurringOccurrencesRecordThePercentOfTheCardWhenTheyAreGenerated()
    {
        await SetCardCashbackAsync(1m);
        Assert.True((await Entries.CreateRecurringAsync(new(Purchase(50)))).Succeeded);
        Assert.Equal(1m, Assert.Single(Assert.Single(await Catalog.GetMonthAsync(s_october)).Entries).CashbackPercent);

        await SetCardCashbackAsync(2m);

        Assert.Equal(1m, Assert.Single(Assert.Single(await Catalog.GetMonthAsync(s_october)).Entries).CashbackPercent);
        Assert.Equal(2m, Assert.Single(Assert.Single(await Catalog.GetMonthAsync(s_november)).Entries).CashbackPercent);
    }

    [Fact]
    public async Task CardWithoutCashbackAndInvoiceAdjustmentsGenerateNoExpectedCashback()
    {
        var plain = (await Entries.CreateAsync(Purchase(100))).ExternalId!.Value;
        Assert.Equal(0m, (await EntryAsync(plain)).CashbackPercent);
        Assert.Empty(await Cashback.GetMonthAsync(s_october));

        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(100));
        var adjusted = await Entries.AdjustInvoiceAsync(new(Guid.CreateVersion7(), _cardId, s_october, new Money(500)));
        Assert.True(adjusted.Succeeded, adjusted.Error);

        var invoice = Assert.Single(await Catalog.GetMonthAsync(s_october));
        Assert.Equal(new Money(500), invoice.Total);
        Assert.Equal(new Money(1m), invoice.ExpectedCashback);
    }

    [Fact]
    public async Task PanelShowsExpectedUntilPaidAndOnlyAPaidInvoiceCanBeReceived()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));

        var expected = Assert.Single(await Cashback.GetMonthAsync(s_october));
        Assert.Equal(CashbackStatus.Expected, expected.Status);
        Assert.Equal(new Money(12m), expected.Expected);
        Assert.False((await Cashback.ReceiveAsync(Receive(12))).Succeeded);
        Assert.Empty(await Cashback.GetMonthAsync(s_november));

        await PayOctoberAsync();

        Assert.Equal(CashbackStatus.ToReceive, Assert.Single(await Cashback.GetMonthAsync(s_october)).Status);
    }

    [Fact]
    public async Task ReceivingInTheAccountCreatesOneProtectedIncomeAndUndoRestoresTheBalance()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));
        await PayOctoberAsync();
        var date = new DateOnly(2026, 10, 15);
        var before = (await Entries.GetBalanceAsync(_accountId, date))!.EffectiveBalance;
        var request = Receive(11.40m);

        var results = await Task.WhenAll(Cashback.ReceiveAsync(request), Cashback.ReceiveAsync(request));

        Assert.All(results, x => Assert.True(x.Succeeded, x.Error));
        Assert.Equal(before + new Money(11.40m), (await Entries.GetBalanceAsync(_accountId, date))!.EffectiveBalance);
        var income = Assert.Single(await Entries.GetAsync(), x => x.Kind == CategoryKind.Income);
        Assert.Equal("Cashback > Cashback recebido", income.Classification);
        Assert.Equal("Cashback Cartão Exemplo", income.MerchantName);
        var item = Assert.Single(await Cashback.GetMonthAsync(s_october));
        Assert.Equal(CashbackStatus.Received, item.Status);
        Assert.Equal(new Money(12m), item.Receipt!.Expected);
        Assert.Equal(new Money(11.40m), item.Receipt.Received);
        Assert.False((await Cashback.ReceiveAsync(Receive(12))).Succeeded);

        var edit = await Entries.EditAsync(income.ExternalId, new(date, income.MerchantExternalId, income.SubcategoryExternalId, _accountId, new Money(20)), EntryChangeScope.OnlyThis);
        Assert.False(edit.Succeeded);
        Assert.Contains("cashback", edit.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False((await Entries.DeleteAsync(income.ExternalId, EntryChangeScope.OnlyThis)).Succeeded);

        var undone = await Cashback.UndoAsync(item.Receipt.ExternalId);

        Assert.True(undone.Succeeded, undone.Error);
        Assert.Equal(before, (await Entries.GetBalanceAsync(_accountId, date))!.EffectiveBalance);
        Assert.DoesNotContain(await Entries.GetAsync(), x => x.Kind == CategoryKind.Income);
        Assert.Equal(CashbackStatus.ToReceive, Assert.Single(await Cashback.GetMonthAsync(s_october)).Status);
    }

    [Fact]
    public async Task ZeroClosesTheCashbackWithoutEntryAndNegativeOrMissingDestinationIsRejected()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));
        await PayOctoberAsync();
        var entries = (await Entries.GetAsync()).Count;

        Assert.False((await Cashback.ReceiveAsync(Receive(-1))).Succeeded);
        Assert.False((await Cashback.ReceiveAsync(Receive(5, CashbackDestination.None))).Succeeded);
        Assert.False((await Cashback.ReceiveAsync(Receive(5) with { AccountExternalId = null })).Succeeded);

        var closed = await Cashback.ReceiveAsync(Receive(0, CashbackDestination.None) with { AccountExternalId = null });

        Assert.True(closed.Succeeded, closed.Error);
        Assert.Equal(entries, (await Entries.GetAsync()).Count);
        var item = Assert.Single(await Cashback.GetMonthAsync(s_october));
        Assert.Equal(CashbackStatus.Received, item.Status);
        Assert.Equal(CashbackDestination.None, item.Receipt!.Destination);
        Assert.Empty(await Cashback.GetHistoryAsync(2026));
    }

    [Fact]
    public async Task FailedIncomeLeavesNoReceipt()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));
        await PayOctoberAsync();
        var merchants = Provider.GetRequiredService<IMerchantCatalog>();
        var merchant = await merchants.SaveAsync(null, "Cashback Cartão Exemplo", _subcategoryId);
        Assert.True((await merchants.DeactivateAsync(merchant.ExternalId!.Value)).Succeeded);

        var failed = await Cashback.ReceiveAsync(Receive(12));

        Assert.False(failed.Succeeded);
        Assert.Equal(CashbackStatus.ToReceive, Assert.Single(await Cashback.GetMonthAsync(s_october)).Status);
        Assert.DoesNotContain(await Entries.GetAsync(), x => x.Kind == CategoryKind.Income);
    }

    [Fact]
    public async Task AllowanceOnAnOpenInvoiceReducesItsBalanceWithoutIncomeAndUndoIsBlockedWhenTheTargetIsPaid()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));
        await Entries.CreateAsync(Purchase(100, 11));
        await PayOctoberAsync();
        var targets = await Cashback.GetTargetInvoicesAsync(_cardId, new DateOnly(2026, 9, 1));
        Assert.Contains(s_november, targets);
        Assert.DoesNotContain(s_october, targets);
        Assert.False((await Cashback.ReceiveAsync(Receive(11.40m, CashbackDestination.Invoice, s_october))).Succeeded);

        var received = await Cashback.ReceiveAsync(Receive(11.40m, CashbackDestination.Invoice, s_november));

        Assert.True(received.Succeeded, received.Error);
        Assert.DoesNotContain(await Entries.GetAsync(), x => x.Kind == CategoryKind.Income);
        var november = Assert.Single(await Catalog.GetMonthAsync(s_november));
        Assert.Equal(new Money(11.40m), november.CashbackTotal);
        Assert.Equal(new Money(88.60m), november.RemainingBalance);
        var allowance = Assert.Single(november.PaymentHistory);
        Assert.False(allowance.IsManualCashback);
        Assert.Contains("10/2026", allowance.Details, StringComparison.Ordinal);
        Assert.False((await Payments.SaveAllowanceAsync(allowance.ExternalId, new(allowance.ConfirmationId, _cardId, s_november, allowance.Date, new Money(1), InvoicePaymentKind.Discount))).Succeeded);
        Assert.False((await Payments.DeleteAllowanceAsync(allowance.ExternalId)).Succeeded);
        var item = Assert.Single(await Cashback.GetMonthAsync(s_october));
        Assert.Equal(s_november, item.Receipt!.TargetMonth);
        var history = Assert.Single(await Cashback.GetHistoryAsync(2026));
        Assert.Equal(CashbackDestination.Invoice, history.Destination);

        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("UPDATE invoices.invoices SET \"Status\" = 2 WHERE \"Month\" = 202611");
        Assert.False((await Cashback.UndoAsync(item.Receipt.ExternalId)).Succeeded);
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("UPDATE invoices.invoices SET \"Status\" = 0 WHERE \"Month\" = 202611");

        Assert.True((await Cashback.UndoAsync(item.Receipt.ExternalId)).Succeeded);
        Assert.Equal(new Money(100), Assert.Single(await Catalog.GetMonthAsync(s_november)).RemainingBalance);
        Assert.Equal(CashbackStatus.ToReceive, Assert.Single(await Cashback.GetMonthAsync(s_october)).Status);
    }

    [Fact]
    public async Task AClosedTargetInvoiceCannotReceiveTheAllowance()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));
        await Entries.CreateAsync(Purchase(100, 11));
        await PayOctoberAsync();
        await Lifecycle.CloseAsync(_cardId, s_november);

        Assert.False((await Cashback.ReceiveAsync(Receive(11.40m, CashbackDestination.Invoice, s_november))).Succeeded);
        Assert.Equal(CashbackStatus.ToReceive, Assert.Single(await Cashback.GetMonthAsync(s_october)).Status);
    }

    [Fact]
    public async Task ReopeningThePaidSourceInvoiceKeepsTheReceipt()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));
        await PayOctoberAsync();
        Assert.True((await Cashback.ReceiveAsync(Receive(12))).Succeeded);

        Assert.True((await Payments.ReopenUndoingPaymentsAsync(_cardId, s_october)).Succeeded);

        var item = Assert.Single(await Cashback.GetMonthAsync(s_october));
        Assert.Equal(InvoiceStatus.Open, item.InvoiceStatus);
        Assert.Equal(CashbackStatus.Received, item.Status);
        Assert.Single(await Entries.GetAsync(), x => x.Kind == CategoryKind.Income);
    }

    [Fact]
    public async Task YearHistoryIncludesReceiptsAndLegacyManualCashback()
    {
        await SetCardCashbackAsync(1m);
        await Entries.CreateAsync(Purchase(1200));
        await PayOctoberAsync();
        Assert.True((await Payments.SaveAllowanceAsync(null, Allowance(8) with { Month = s_november, Date = new DateOnly(2026, 9, 2) })).Succeeded);
        await (_database ?? throw new InvalidOperationException()).ExecuteAsync("UPDATE invoices.invoice_payments SET \"Kind\" = 1 WHERE \"Kind\" = 2");
        Assert.True((await Cashback.ReceiveAsync(Receive(12))).Succeeded);

        var history = await Cashback.GetHistoryAsync(2026);

        Assert.Equal(2, history.Count);
        Assert.Equal(new Money(20), new Money(history.Sum(x => x.Amount.Amount)));
        Assert.Single(history, x => x.IsManual && x.Month == s_november);
        Assert.Single(history, x => !x.IsManual && x.Destination == CashbackDestination.Account);
        Assert.Empty(await Cashback.GetHistoryAsync(2025));
    }
}
