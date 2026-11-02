using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class AccountTransferPageTests
{
    [Fact]
    public async Task TransferRequiresConfirmationAndDisplaysBothLegsAndBalances()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var today = DateOnly.FromDateTime(DateTime.Now);
        var source = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Corrente Exemplo", AccountKind.Checking, "green", new Money(1000), today));
        var destination = await accounts.SaveAccountAsync(new(null, bank.ExternalId.Value, "Reserva Exemplo", AccountKind.Investment, "green"));
        var page = context.Render<AccountTransfers>();
        page.WaitForAssertion(() => Assert.Contains("Corrente Exemplo", page.Find("#transfer-source").TextContent, StringComparison.Ordinal));
        page.Find("#transfer-source").Change(source.ExternalId!.Value.ToString());
        page.Find("#transfer-destination").Change(destination.ExternalId!.Value.ToString());
        page.Find("#transfer-amount").Change("300");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("R$ 300,00", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal);
        page.FindAll("[role=alertdialog] button")[1].Click();
        var movements = context.Services.GetRequiredService<IAccountMovementCatalog>();
        Assert.Empty(await movements.GetAsync());
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        await page.Find("[role=alertdialog] button").ClickAsync(new());
        Assert.Equal(2, page.FindAll("[aria-label='Movimentações de contas'] li").Count);
        Assert.Contains("R$ 700,00", page.Markup, StringComparison.Ordinal);
        Assert.Equal(2, (await movements.GetAsync()).Count);
    }
}
