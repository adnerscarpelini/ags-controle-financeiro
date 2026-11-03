using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components;
using Bunit;

namespace Ags.Finance.Web.Tests;

public sealed class CreditCardViewTests
{
    [Fact]
    public async Task WalletUsesLocalLogoRegisteredFinalsAndAccessibleActions()
    {
        await using var context = new BunitContext();
        var bank = new BankDetails(Guid.CreateVersion7(), "Banco Exemplo", Guid.CreateVersion7());
        var account = new AccountDetails(Guid.CreateVersion7(), bank.ExternalId, "Conta Exemplo", AccountKind.Checking, "green", true);
        var card = new CreditCardDetails(Guid.CreateVersion7(), account.ExternalId, "Cartão Principal", CardBrand.Visa, new Money(1234.56m), 31, 10, true, "green", true);
        var finals = new[] { new CardLastDigitsDetails(Guid.CreateVersion7(), card.ExternalId, "1234", CardUsage.Own, "Titular", true) };
        var edited = false;
        var consulted = false;
        var archived = false;
        var view = context.Render<CreditCardView>(parameters => parameters.Add(x => x.Card, card).Add(x => x.Bank, bank)
            .Add(x => x.Account, account).Add(x => x.Finals, finals).Add(x => x.Edit, () => edited = true)
            .Add(x => x.ShowFinals, () => consulted = true).Add(x => x.ToggleArchive, () => archived = true));
        Assert.Equal($"/bank-logos/{bank.ExternalId}?v={bank.LogoVersion:N}", view.Find("img").GetAttribute("src"));
        Assert.Contains("Banco Exemplo", view.Find("article").TextContent, StringComparison.Ordinal);
        Assert.Contains("Conta Exemplo", view.Find("article").TextContent, StringComparison.Ordinal);
        Assert.Contains("Final 1234 · Titular", view.Find("article").TextContent, StringComparison.Ordinal);
        Assert.Contains("R$ 1.234,56", view.Find("dl").TextContent, StringComparison.Ordinal);
        Assert.Contains("Dia 31", view.Find("dl").TextContent, StringComparison.Ordinal);
        Assert.Contains("Dia 10", view.Find("dl").TextContent, StringComparison.Ordinal);
        Assert.Empty(view.FindAll("input"));
        foreach (var button in view.FindAll("button")) { await button.ClickAsync(new()); }
        Assert.True(edited && consulted && archived);
    }

    [Theory]
    [InlineData(false, true, "Arquivado individualmente", "Desarquivar")]
    [InlineData(true, false, "Indisponível: conta arquivada", "Arquivar")]
    public void WalletKeepsTextualIdentificationAndExplainsUnavailableState(bool active, bool accountActive, string status, string action)
    {
        using var context = new BunitContext();
        var card = new CreditCardDetails(Guid.CreateVersion7(), Guid.CreateVersion7(), "Cartão Exemplo", CardBrand.Elo, new Money(500), 5, 12, false, "violet", active, accountActive);
        var view = context.Render<CreditCardView>(parameters => parameters.Add(x => x.Card, card).Add(x => x.Bank, new BankDetails(Guid.CreateVersion7(), "Banco Exemplo")));
        Assert.Equal("BE", view.Find(".bank-logo-initials").TextContent);
        Assert.Contains("Sem final cadastrado", view.Find("article").TextContent, StringComparison.Ordinal);
        Assert.Equal(status, view.Find(".wallet-card-status").TextContent);
        Assert.Equal(action, view.FindAll("button")[2].TextContent);
    }
}
