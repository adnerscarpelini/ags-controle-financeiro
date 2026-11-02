using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class AccountsAndCardsTests : IAsyncLifetime, IDisposable
{
    private readonly BunitContext _context = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("ags-catalog-ui-").FullName;

    public async Task InitializeAsync()
    {
        _context.Services.AddAccountsModule($"Data Source={Path.Combine(_directory, "finance.db")}");
        await _context.Services.MigrateAccountsModuleAsync();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _context.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task FormsCreateBankAccountAndCardAndOfferDeactivation()
    {
        var page = _context.Render<AccountsAndCards>();
        page.Find("#bank-name").Change("Banco Exemplo");
        page.FindAll("form")[0].Submit();
        page.WaitForAssertion(() => Assert.Contains("Banco Exemplo", page.Find("ul").TextContent, StringComparison.Ordinal));
        var catalog = _context.Services.GetRequiredService<IAccountCatalog>();
        var bank = Assert.Single(await catalog.GetBanksAsync());
        page.Find("#account-bank").Change(bank.ExternalId.ToString());
        page.Find("#account-name").Change("Conta Exemplo");
        page.Find("#account-color").Change("green");
        page.FindAll("form")[1].Submit();
        page.WaitForAssertion(() => Assert.Contains("Conta Exemplo", page.Find(".catalog-list").TextContent, StringComparison.Ordinal));
        var account = Assert.Single(await catalog.GetAccountsAsync());
        page.Find("#card-name").Change("Cartão Exemplo");
        page.Find("#card-account").Change(account.ExternalId.ToString());
        page.Find("#card-brand").Change(CardBrand.Mastercard.ToString());
        page.Find("#card-limit").Change("10000");
        page.Find("#card-color").Change("violet");
        page.FindAll("form")[2].Submit();
        page.WaitForAssertion(() => Assert.Contains("Mastercard", page.FindAll(".catalog-list")[1].TextContent, StringComparison.Ordinal));
        page.FindAll(".catalog-list")[1].QuerySelectorAll("button")[1].Click();
        page.WaitForAssertion(() => Assert.Contains("Desativar em vez de excluir", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal));
        page.Find("[role=alertdialog] button").Click();
        page.WaitForAssertion(() => Assert.Contains("Desativado", page.FindAll(".catalog-list")[1].TextContent, StringComparison.Ordinal));
        Assert.Empty(await catalog.GetCreditCardsAsync());
        Assert.Single(await catalog.GetCreditCardsAsync(false));
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
