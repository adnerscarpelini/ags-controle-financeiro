using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed partial class CatalogTests
{
    [Fact]
    public async Task ReferenceMonthContractUsesThePersistedCardCalendarWithoutWritingTransactions()
    {
        var calculator = Provider.GetRequiredService<IReferenceMonthCalculator>();
        var card = await Catalog.SaveCreditCardAsync(Card(await CreateAccountAsync()));
        var result = await calculator.CalculateAsync(new DateOnly(2026, 9, 14), card.ExternalId);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(new ReferenceMonth(2026, 10), result.Month);

        await Catalog.SaveCreditCardAsync(Card((await Catalog.GetAccountsAsync())[0].ExternalId) with { ExternalId = card.ExternalId, ClosingDay = 25, DueDay = 5 });
        var updated = await calculator.CalculateAsync(new DateOnly(2026, 9, 26), card.ExternalId);
        Assert.Equal(new ReferenceMonth(2026, 11), updated.Month);
        var debit = await calculator.CalculateAsync(new DateOnly(2026, 9, 14));
        Assert.Equal(new ReferenceMonth(2026, 9), debit.Month);
        Assert.Single(await Catalog.GetCreditCardsAsync());
    }

    [Fact]
    public async Task ReferenceMonthContractReturnsExpectedFailuresAndSupportsHistoricalCards()
    {
        var calculator = Provider.GetRequiredService<IReferenceMonthCalculator>();
        var missing = await calculator.CalculateAsync(new DateOnly(2026, 9, 14), Guid.NewGuid());
        Assert.False(missing.Succeeded);
        Assert.Null(missing.Month);
        Assert.Equal("Cartão não encontrado.", missing.Error);

        var card = await Catalog.SaveCreditCardAsync(Card(await CreateAccountAsync()));
        await Catalog.DeactivateCreditCardAsync(card.ExternalId!.Value);
        Assert.True((await calculator.CalculateAsync(new DateOnly(2026, 9, 14), card.ExternalId)).Succeeded);
        var overflow = await calculator.CalculateAsync(DateOnly.MaxValue, card.ExternalId);
        Assert.False(overflow.Succeeded);
        Assert.Null(overflow.Month);
    }
}
