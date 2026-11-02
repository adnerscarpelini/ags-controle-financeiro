using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class AccountsAndCardsTests : IAsyncLifetime, IDisposable
{
    private readonly BunitContext _context = new();
    private PostgresTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        _context.Services.AddAccountsModule(_database.ConnectionString);
        await _context.Services.MigrateAccountsModuleAsync();
    }

    public async Task DisposeAsync()
    {
        _context.Dispose();
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task FormsCreateBankAccountAndCardAndOfferDeactivation()
    {
        var page = _context.Render<AccountsAndCards>();
        page.Find("#bank-name").Change("Banco Exemplo");
        await page.FindAll("form")[0].SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("Banco Exemplo", page.Find("ul").TextContent, StringComparison.Ordinal));
        var catalog = _context.Services.GetRequiredService<IAccountCatalog>();
        var bank = Assert.Single(await catalog.GetBanksAsync());
        page.Find("#account-bank").Change(bank.ExternalId.ToString());
        page.Find("#account-name").Change("Conta Exemplo");
        page.Find("#account-color").Change("green");
        await page.FindAll("form")[1].SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("Conta Exemplo", page.Find(".catalog-list").TextContent, StringComparison.Ordinal));
        var account = Assert.Single(await catalog.GetAccountsAsync());
        page.Find("#card-name").Change("Cartão Exemplo");
        page.Find("#card-account").Change(account.ExternalId.ToString());
        page.Find("#card-brand").Change(CardBrand.Mastercard.ToString());
        page.Find("#card-limit").Change("10000");
        page.Find("#card-color").Change("violet");
        await page.FindAll("form")[2].SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("Mastercard", page.FindAll(".catalog-list")[1].TextContent, StringComparison.Ordinal));
        page.FindAll(".catalog-list")[1].QuerySelectorAll("button")[2].Click();
        page.WaitForAssertion(() => Assert.Contains("Desativar em vez de excluir", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal));
        page.Find("[role=alertdialog] button").Click();
        page.WaitForAssertion(() => Assert.Contains("Desativado", page.FindAll(".catalog-list")[1].TextContent, StringComparison.Ordinal));
        Assert.Empty(await catalog.GetCreditCardsAsync());
        Assert.Single(await catalog.GetCreditCardsAsync(false));
    }

    [Fact]
    public async Task FinalFormCreatesSubscriptionAndConfirmsDeactivation()
    {
        var catalog = _context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await catalog.CreateBankAsync("Banco Exemplo");
        var account = await catalog.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        var card = await catalog.SaveCreditCardAsync(new(null, account.ExternalId!.Value, "Cartão Exemplo", CardBrand.Visa, new Money(1000), 3, 10, false, "green"));
        var panel = _context.Render<CardLastDigitsPanel>(parameters => parameters
            .Add(component => component.CardExternalId, card.ExternalId!.Value)
            .Add(component => component.CardName, "Cartão Exemplo"));

        // A carga inicial da lista termina depois da primeira renderização e a refaz; repete até a árvore estabilizar.
        panel.WaitForAssertion(() =>
        {
            panel.Find("#card-digits").Change("1234");
            panel.Find("#digits-usage").Change(CardUsage.Subscription.ToString());
            panel.Find("#digits-label").Change("Serviços");
            panel.Find("form").Submit();
        });
        panel.WaitForAssertion(() => Assert.Contains("1234 · Assinatura · Serviços", panel.Find(".catalog-list").TextContent, StringComparison.Ordinal));

        panel.FindAll(".catalog-list button")[1].Click();
        panel.WaitForAssertion(() => Assert.Contains("preserva os lançamentos", panel.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal));
        panel.Find("[role=alertdialog] button").Click();
        panel.WaitForAssertion(() => Assert.Contains("Desativado", panel.Find(".catalog-list").TextContent, StringComparison.Ordinal));
        Assert.Empty(await _context.Services.GetRequiredService<ICardLastDigitsCatalog>().GetAsync(card.ExternalId!.Value));
    }

    [Fact]
    public void InvalidInputShowsBusinessValidationMessage()
    {
        var page = _context.Render<AccountsAndCards>();
        page.Find("#account-name").Change("Conta sem banco");
        page.FindAll("form")[1].Submit();
        page.WaitForAssertion(() => Assert.Contains("banco cadastrado", page.Find("[role=status]").TextContent, StringComparison.Ordinal));
    }
}
