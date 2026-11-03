using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

public sealed class CreditCardViewTests
{
    [Fact]
    public async Task WalletShowsIdentificationInvoiceAndLimitAndTheMenuRunsTheActions()
    {
        await using var context = new BunitContext().UseAppUi();
        var popovers = context.Render<MudPopoverProvider>();
        var bank = new BankDetails(Guid.CreateVersion7(), "Banco Exemplo", Guid.CreateVersion7());
        var account = new AccountDetails(Guid.CreateVersion7(), bank.ExternalId, "Conta Exemplo", AccountKind.Checking, "green", true);
        var card = new CreditCardDetails(Guid.CreateVersion7(), account.ExternalId, "Cartão Principal", CardBrand.Visa, new Money(1234.56m), 31, 10, true, "green", true);
        var finals = new[] { new CardLastDigitsDetails(Guid.CreateVersion7(), card.ExternalId, "1234", CardUsage.Own, "Titular", true) };
        var invoice = new InvoiceDetails(card.ExternalId, card.Name, new ReferenceMonth(2026, 11), new Money(100), []);
        var actions = new List<string>();
        var view = context.Render<CreditCardView>(parameters => parameters.Add(x => x.Card, card).Add(x => x.Bank, bank)
            .Add(x => x.Account, account).Add(x => x.Finals, finals).Add(x => x.Invoice, invoice).Add(x => x.DueDate, new DateOnly(2026, 11, 10))
            .Add(x => x.Limit, new CardLimit(card.ExternalId, card.CreditLimit, new Money(100)))
            .Add(x => x.Edit, () => actions.Add("edit")).Add(x => x.ShowFinals, () => actions.Add("finals"))
            .Add(x => x.History, () => actions.Add("history")).Add(x => x.ToggleArchive, () => actions.Add("archive")));

        Assert.Equal($"/bank-logos/{bank.ExternalId}?v={bank.LogoVersion:N}", view.Find("img").GetAttribute("src"));
        var text = view.Find("article").TextContent;
        Assert.Contains("Banco Exemplo", text, StringComparison.Ordinal);
        Assert.Contains("Conta Exemplo", text, StringComparison.Ordinal);
        Assert.Contains("Final 1234 · Titular", text, StringComparison.Ordinal);
        Assert.Equal("Fatura aberta", view.Find("[data-invoice-status]").TextContent);
        Assert.Equal("R$ 100,00", view.Find("[data-invoice-total]").TextContent);
        Assert.Equal("10/11/2026", view.Find("[data-invoice-due]").TextContent);
        Assert.Contains("R$ 1.234,56", view.Find("[data-card-limit]").TextContent, StringComparison.Ordinal);
        Assert.Empty(view.FindAll("input"));

        foreach (var item in new[] { "Editar", "Finais do cartão", "Histórico de faturas", "Arquivar" })
        {
            await view.Find("[data-card-menu] button").ClickAsync(new MouseEventArgs());
            popovers.WaitForAssertion(() => Assert.Contains(popovers.FindAll(".mud-popover-open .mud-menu-item"), x => x.TextContent.Trim() == item));
            await popovers.FindAll(".mud-popover-open .mud-menu-item").Single(x => x.TextContent.Trim() == item).ClickAsync(new MouseEventArgs());
        }

        Assert.Equal(["edit", "finals", "history", "archive"], actions);
    }

    [Theory]
    [InlineData(false, true, "Arquivado individualmente")]
    [InlineData(true, false, "Indisponível: conta arquivada")]
    public async Task WalletKeepsTextualIdentificationAndExplainsUnavailableState(bool active, bool accountActive, string status)
    {
        await using var context = new BunitContext().UseAppUi();
        var card = new CreditCardDetails(Guid.CreateVersion7(), Guid.CreateVersion7(), "Cartão Exemplo", CardBrand.Elo, new Money(500), 5, 12, false, "violet", active, accountActive);

        var view = context.Render<CreditCardView>(parameters => parameters.Add(x => x.Card, card).Add(x => x.Bank, new BankDetails(Guid.CreateVersion7(), "Banco Exemplo")));

        Assert.Equal("BE", view.Find(".bank-logo-initials").TextContent);
        Assert.Contains("Sem final cadastrado", view.Find("article").TextContent, StringComparison.Ordinal);
        Assert.Equal(status, view.Find("[data-card-status]").TextContent);
        Assert.Contains("entity-card-archived", view.Find("article").ClassList);
    }
}
