using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task CashDateChangesRecalculateMonthAndIgnoreManualInput()
    {
        var result = await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 9, 14), Month = new ReferenceMonth(2027, 1) });
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.Equal(new ReferenceMonth(2026, 9), entry.Month);
        Assert.True((await Entries.EditAsync(result.ExternalId!.Value, Change(entry) with { Date = new DateOnly(2026, 10, 5) }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal(new ReferenceMonth(2026, 10), Assert.Single(await Entries.GetAsync()).Month);
    }

    [Fact]
    public async Task CardCalendarAndSelectedFirstInvoiceKeepInstallmentsConsecutiveAndCalendarEditsKeepSavedMonths()
    {
        var accounts = Provider.GetRequiredService<IAccountCatalog>();
        var card = (await accounts.SaveCreditCardAsync(new(null, _accountId, "Calendário", CardBrand.Visa, new Money(10000), 1, 9, true, "green"))).ExternalId!.Value;
        var request = Request() with { Date = new DateOnly(2026, 11, 3), CardExternalId = card, Amount = new Money(300) };
        Assert.True((await Entries.CreateInstallmentsAsync(request, 3)).Succeeded);
        var entries = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal([new ReferenceMonth(2026, 12), new ReferenceMonth(2027, 1), new ReferenceMonth(2027, 2)], entries.Select(x => x.Month));
        Assert.True((await Entries.EditAsync(entries[0].ExternalId, Change(entries[0]) with { Month = new ReferenceMonth(2027, 1) }, EntryChangeScope.ThisAndFollowing)).Succeeded);
        var shifted = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.Equal([new ReferenceMonth(2027, 1), new ReferenceMonth(2027, 2), new ReferenceMonth(2027, 3)], shifted.Select(x => x.Month));
        Assert.All(shifted, x => Assert.Equal(request.Date, x.Date));
        Assert.True((await accounts.SaveCreditCardAsync(new(card, _accountId, "Calendário", CardBrand.Visa, new Money(10000), 20, 25, true, "green"))).Succeeded);
        Assert.Equal(shifted.Select(x => x.Month), (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).Select(x => x.Month));
        Assert.True((await Entries.EditAsync(shifted[0].ExternalId, Change(shifted[0]) with { Details = "Corrigido" }, EntryChangeScope.OnlyThis)).Succeeded);
        Assert.Equal(shifted[0].Month, (await Entries.GetAsync()).Single(x => x.ExternalId == shifted[0].ExternalId).Month);
    }
}
