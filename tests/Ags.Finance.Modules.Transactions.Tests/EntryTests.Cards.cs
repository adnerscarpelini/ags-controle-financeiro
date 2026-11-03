using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private async Task<Guid> CreateCardAsync()
    {
        var result = await Provider.GetRequiredService<IAccountCatalog>().SaveCreditCardAsync(new(null, _accountId, "Cartão Exemplo", CardBrand.Mastercard, new Money(10000), 3, 10, true, "green"));
        Assert.True(result.Succeeded, result.Error);
        return result.ExternalId!.Value;
    }

    [Fact]
    public async Task CardSuggestsDueMonthKeepsManualChoiceAndDoesNotDebitCash()
    {
        var card = await CreateCardAsync();
        var first = await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 9, 14), CardExternalId = card, AccountExternalId = Guid.NewGuid() });
        Assert.True(first.Succeeded, first.Error);
        var entry = Assert.Single(await Entries.GetAsync());
        Assert.Equal(new ReferenceMonth(2026, 10), entry.Month);
        Assert.Equal(_accountId, entry.AccountExternalId);
        var second = await Entries.CreateAsync(Request() with { Date = new DateOnly(2026, 10, 2), CardExternalId = card, Month = new ReferenceMonth(2026, 11) });
        Assert.True(second.Succeeded, second.Error);
        var manual = (await Entries.GetAsync()).Single(x => x.ExternalId == second.ExternalId);
        Assert.Equal(new DateOnly(2026, 10, 2), manual.Date);
        Assert.Equal(new ReferenceMonth(2026, 11), manual.Month);
        Assert.Equal(new Money(1000), (await Entries.GetBalanceAsync(_accountId, new DateOnly(2026, 12, 31)))?.EffectiveBalance);
    }

    [Fact]
    public async Task FinalMustBelongToSelectedActiveCardAndRemainTraceable()
    {
        var card = await CreateCardAsync();
        var otherCard = await CreateCardAsync();
        var digits = Provider.GetRequiredService<ICardLastDigitsCatalog>();
        var final = await digits.SaveAsync(new(null, card, "1234", CardUsage.Own, "Próprio"));
        var request = Request() with { CardExternalId = otherCard, CardLastDigitsExternalId = final.ExternalId };
        Assert.False((await Entries.CreateAsync(request)).Succeeded);
        Assert.Empty(await Entries.GetAsync());
        Assert.True((await Entries.CreateAsync(request with { CardExternalId = card })).Succeeded);
        Assert.Equal(final.ExternalId, Assert.Single(await Entries.GetAsync()).CardLastDigitsExternalId);
        await digits.DeactivateAsync(final.ExternalId!.Value);
        Assert.False((await Entries.CreateAsync(request with { CardExternalId = card })).Succeeded);
        Assert.False((await Entries.CreateAsync(request with { CardExternalId = null })).Succeeded);
    }

    [Fact]
    public async Task UnknownAndUnavailableCardsAreRejected()
    {
        Assert.False((await Entries.CreateAsync(Request() with { CardExternalId = Guid.NewGuid() })).Succeeded);
        var card = await CreateCardAsync();
        await Provider.GetRequiredService<IAccountCatalog>().DeactivateAccountAsync(_accountId);
        Assert.False((await Entries.CreateAsync(Request() with { CardExternalId = card })).Succeeded);
        Assert.Empty(await Entries.GetAsync());
    }
}
