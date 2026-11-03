using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class CardLastDigitsPanelTests : IAsyncLifetime, IDisposable
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
}
