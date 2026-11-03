using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Invoices.Tests;

public sealed partial class InvoiceTests
{
    private static CreateEntryRequest Change(EntryDetails entry) => new(entry.Date, entry.MerchantExternalId, entry.SubcategoryExternalId,
        entry.AccountExternalId, new Money(Math.Abs(entry.Amount.Amount)), entry.Details, entry.Month, entry.CardExternalId, entry.CardLastDigitsExternalId, entry.Reimbursement);

    [Fact]
    public async Task ClosedInvoiceBlocksChangingTheReimbursementOfAPurchase()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        var source = Assert.Single(await entries.GetAsync());
        await Lifecycle.CloseAsync(_cardId, s_october);
        Assert.False((await entries.EditAsync(source.ExternalId, Change(source) with { Reimbursement = new(Guid.CreateVersion7(), 50m) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Null(Assert.Single(await entries.GetAsync()).Reimbursement);
    }

    [Fact]
    public async Task MovingRequiresBothSourceAndDestinationOpenAndPreservesPurchaseData()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateAsync(Purchase(100));
        var source = Assert.Single(await entries.GetAsync());
        await Lifecycle.CloseAsync(_cardId, s_october);
        Assert.False((await entries.EditAsync(source.ExternalId, Change(source) with { Amount = new Money(200) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False((await entries.DeleteAsync(source.ExternalId, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.False((await entries.EditAsync(source.ExternalId, Change(source) with { Month = new ReferenceMonth(2026, 11) }, EntryChangeScope.OnlyThis)).Succeeded);
        await Lifecycle.ReopenAsync(_cardId, s_october);
        await Lifecycle.CloseAsync(_cardId, new ReferenceMonth(2026, 11));
        Assert.False((await entries.EditAsync(source.ExternalId, Change(source) with { Month = new ReferenceMonth(2026, 11) }, EntryChangeScope.OnlyThis)).Succeeded);
        await Lifecycle.ReopenAsync(_cardId, new ReferenceMonth(2026, 11));
        Assert.True((await entries.EditAsync(source.ExternalId, Change(source) with { Month = new ReferenceMonth(2026, 11) }, EntryChangeScope.OnlyThis)).Succeeded);
        var moved = Assert.Single(await entries.GetAsync());
        Assert.Equal(source.ExternalId, moved.ExternalId);
        Assert.Equal(source.Date, moved.Date);
        Assert.Equal(source.Amount, moved.Amount);
        Assert.Equal(new ReferenceMonth(2026, 11), moved.Month);
    }

    [Fact]
    public async Task FollowingScopeReachingClosedInvoiceDoesNotPartiallyEditOrDelete()
    {
        var entries = Provider.GetRequiredService<IEntryCatalog>();
        await entries.CreateInstallmentsAsync(Purchase(1000), 3);
        var source = (await entries.GetAsync()).OrderBy(x => x.InstallmentNumber).First();
        await Lifecycle.CloseAsync(_cardId, new ReferenceMonth(2026, 12));
        var target = await Provider.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Outra Loja", null);
        Assert.False((await entries.EditAsync(source.ExternalId, Change(source) with { MerchantExternalId = target.ExternalId!.Value }, EntryChangeScope.ThisAndFollowing)).Succeeded);
        Assert.False((await entries.DeleteAsync(source.ExternalId, EntryChangeScope.ThisAndFollowing)).Succeeded);
        var preserved = await entries.GetAsync();
        Assert.Equal(3, preserved.Count);
        Assert.All(preserved, x => Assert.Equal(source.MerchantExternalId, x.MerchantExternalId));
        Assert.Equal(-1000m, preserved.Sum(x => x.Amount.Amount));
        Assert.Equal(InvoiceStatus.Closed, (await Catalog.GetAsync()).Single(x => x.Month == new ReferenceMonth(2026, 12)).Status);
    }
}
