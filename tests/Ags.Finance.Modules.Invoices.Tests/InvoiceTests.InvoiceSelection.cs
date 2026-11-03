using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    [Theory]
    [InlineData(true, 1, 2026, 11)]
    [InlineData(false, 1, 2026, 12)]
    [InlineData(true, 3, 2026, 12)]
    public async Task InvoiceSelectionUsesTheCardCalendarAndUnrecordedFutureInvoicesAreOpen(bool includesClosing, int day, int year, int month)
    {
        _clock.Now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var card = (await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, _accountId, "Calendário", CardBrand.Visa, new Money(10000), 1, 9, includesClosing, "green"))).ExternalId!.Value;
        var selection = await Catalog.GetSelectableInvoicesAsync(card, new DateOnly(2026, 11, day));
        Assert.Null(selection.Error);
        Assert.Equal(new AccountingMonth(year, month), selection.CalculatedMonth);
        Assert.Equal(selection.CalculatedMonth, selection.SuggestedMonth);
        Assert.All(selection.Invoices, x => Assert.Equal(InvoiceStatus.Open, x.Status));
    }

    [Fact]
    public async Task ProtectedCalculatedInvoiceSelectsNextOpenIncludingCutoffWithoutStoredState()
    {
        _clock.Now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var selection = await Catalog.GetSelectableInvoicesAsync(_cardId, new DateOnly(2026, 10, 20));
        Assert.Equal(new AccountingMonth(2026, 11), selection.CalculatedMonth);
        Assert.Equal(new AccountingMonth(2026, 12), selection.SuggestedMonth);
        Assert.Equal(InvoiceStatus.Closed, selection.Invoices.Single(x => x.Month == selection.CalculatedMonth).Status);
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        Assert.False((await entries.CreateAsync(Purchase(100, 11))).Succeeded);
        Assert.True((await entries.CreateAsync(Purchase(100, 12))).Succeeded);
        Assert.Single(await entries.GetAsync());
        Assert.Null((await Catalog.GetSelectableInvoicesAsync(Guid.NewGuid(), new DateOnly(2026, 10, 20))).SuggestedMonth);
    }

    [Fact]
    public async Task AnticipatedInstallmentsKeepTheirInvoiceOnDetailsEditsAndRejectDateAndCardChanges()
    {
        var selected = await AnticipationPurchaseAsync();
        Assert.True((await Provider.GetRequiredService<IInvoiceAnticipationCatalog>().AnticipateAsync(new(Guid.CreateVersion7(), selected.ExternalId, 3, Money.Zero))).Succeeded);
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        var moved = (await entries.GetAsync()).Single(x => x.InstallmentNumber == 10);
        var request = new CreateEntryRequest(moved.Date, moved.MerchantExternalId, moved.SubcategoryExternalId, moved.AccountExternalId, new Money(Math.Abs(moved.Amount.Amount)), "Detalhe", CardExternalId: moved.CardExternalId);
        Assert.True((await entries.EditAsync(moved.ExternalId, request, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal(moved.Month, (await entries.GetAsync()).Single(x => x.ExternalId == moved.ExternalId).Month);
        Assert.Contains("antecipada", (await entries.EditAsync(moved.ExternalId, request with { Date = moved.Date.AddDays(1) }, EntryChangeScope.OnlyThis)).Error);
        Assert.Contains("antecipada", (await entries.EditAsync(moved.ExternalId, request with { CardExternalId = null }, EntryChangeScope.OnlyThis)).Error);
    }
}
