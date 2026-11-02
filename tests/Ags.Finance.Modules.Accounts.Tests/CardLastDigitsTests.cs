using Ags.Finance.Modules.Accounts.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Accounts.Tests;

public sealed partial class CatalogTests
{
    private ICardLastDigitsCatalog DigitsCatalog => Provider.GetRequiredService<ICardLastDigitsCatalog>();

    [Fact]
    public async Task CreatesSubscriptionDigitsAndPreservesLeadingZeroes()
    {
        var card = await Catalog.SaveCreditCardAsync(Card(await CreateAccountAsync()));
        var result = await DigitsCatalog.SaveAsync(new(null, card.ExternalId!.Value, "1234", CardUsage.Subscription, "Serviços"));
        Assert.True(result.Succeeded, result.Error);
        var digits = Assert.Single(await DigitsCatalog.GetAsync(card.ExternalId.Value));
        Assert.Equal("1234", digits.Digits);
        Assert.Equal(CardUsage.Subscription, digits.Usage);
        Assert.Equal("Serviços", digits.Label);
        Assert.True((await DigitsCatalog.SaveAsync(new(null, card.ExternalId.Value, "0012", CardUsage.Own, ""))).Succeeded);
        Assert.Contains(await DigitsCatalog.GetAsync(card.ExternalId.Value), x => x.Digits == "0012");
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData(" 1234")]
    [InlineData("１２３４")]
    [InlineData("-123")]
    public async Task RejectsFinalsWithoutExactlyFourAsciiDigits(string invalidDigits)
    {
        var card = await Catalog.SaveCreditCardAsync(Card(await CreateAccountAsync()));
        var result = await DigitsCatalog.SaveAsync(new(null, card.ExternalId!.Value, invalidDigits, CardUsage.Own, ""));
        Assert.False(result.Succeeded);
        Assert.Contains("4 dígitos", result.Error, StringComparison.Ordinal);
        Assert.Empty(await DigitsCatalog.GetAsync(card.ExternalId.Value, false));
    }

    [Fact]
    public async Task RejectsDuplicateDigitsOnlyWithinTheSameCardEvenWhenInactive()
    {
        var account = await CreateAccountAsync();
        var card = await Catalog.SaveCreditCardAsync(Card(account));
        var otherCard = await Catalog.SaveCreditCardAsync(Card(account) with { Name = "Outro cartão" });
        var request = new SaveCardLastDigitsRequest(null, card.ExternalId!.Value, "1234", CardUsage.Own, "");
        var saved = await DigitsCatalog.SaveAsync(request);
        Assert.False((await DigitsCatalog.SaveAsync(request)).Succeeded);
        Assert.True((await DigitsCatalog.SaveAsync(request with { CardExternalId = otherCard.ExternalId!.Value })).Succeeded);
        await DigitsCatalog.DeactivateAsync(saved.ExternalId!.Value);
        Assert.False((await DigitsCatalog.SaveAsync(request)).Succeeded);
    }

    [Fact]
    public async Task EditsUsageAndLabelAndDeactivatesWithoutLosingHistory()
    {
        var card = await Catalog.SaveCreditCardAsync(Card(await CreateAccountAsync()));
        var request = new SaveCardLastDigitsRequest(null, card.ExternalId!.Value, "1234", CardUsage.Own, "");
        var saved = await DigitsCatalog.SaveAsync(request);
        Assert.True((await DigitsCatalog.SaveAsync(request with { ExternalId = saved.ExternalId, Usage = CardUsage.Shared, Label = "Compartilhado exemplo" })).Succeeded);
        var edited = Assert.Single(await DigitsCatalog.GetAsync(card.ExternalId.Value));
        Assert.Equal(CardUsage.Shared, edited.Usage);
        Assert.Equal("Compartilhado exemplo", edited.Label);
        await DigitsCatalog.DeactivateAsync(saved.ExternalId!.Value);
        Assert.Empty(await DigitsCatalog.GetAsync(card.ExternalId.Value));
        var historical = Assert.Single(await DigitsCatalog.GetAsync(card.ExternalId.Value, false));
        Assert.Equal(saved.ExternalId, historical.ExternalId);
        Assert.False(historical.IsActive);
    }

    [Fact]
    public async Task RejectsMissingCardInvalidUsageAndEditingAnotherCardsFinal()
    {
        var request = new SaveCardLastDigitsRequest(null, Guid.NewGuid(), "1234", CardUsage.Own, "");
        Assert.False((await DigitsCatalog.SaveAsync(request)).Succeeded);
        var account = await CreateAccountAsync();
        var card = await Catalog.SaveCreditCardAsync(Card(account));
        request = request with { CardExternalId = card.ExternalId!.Value };
        Assert.False((await DigitsCatalog.SaveAsync(request with { Usage = (CardUsage)999 })).Succeeded);
        var saved = await DigitsCatalog.SaveAsync(request);
        var otherCard = await Catalog.SaveCreditCardAsync(Card(account));
        Assert.False((await DigitsCatalog.SaveAsync(request with { ExternalId = saved.ExternalId, CardExternalId = otherCard.ExternalId!.Value })).Succeeded);
        Assert.False((await DigitsCatalog.DeactivateAsync(Guid.NewGuid())).Succeeded);
        await Catalog.DeactivateCreditCardAsync(card.ExternalId.Value);
        Assert.Empty(await DigitsCatalog.GetAsync(card.ExternalId.Value));
        Assert.Single(await DigitsCatalog.GetAsync(card.ExternalId.Value, false));
        Assert.False((await DigitsCatalog.SaveAsync(request with { Digits = "4321" })).Succeeded);
    }
}
