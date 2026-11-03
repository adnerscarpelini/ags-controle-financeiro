using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class CashbackPageTests
{
    [Fact]
    public async Task CashbackPageCreatesEditsAndCancelsDeletionAndInvoiceShowsCredit()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString).AddInvoicesModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        var card = await accounts.SaveCreditCardAsync(new(null, account.ExternalId!.Value, "Cartão Exemplo", CardBrand.Visa, new Money(1000), 3, 10, true, "green"));
        var page = context.Render<Cashback>();
        page.WaitForAssertion(() => Assert.Contains("Cartão Exemplo", page.Find("#allowance-card").TextContent, StringComparison.Ordinal));
        page.Find("#allowance-card").Change(card.ExternalId!.Value.ToString());
        page.Find("#allowance-amount").Change("8");
        await page.Find("#allowance-form").SubmitAsync(EventArgs.Empty);
        Assert.Single(page.FindAll("[aria-label='Histórico de abatimentos'] li"));
        await page.Find("[aria-label='Histórico de abatimentos'] button").ClickAsync(new());
        page.Find("#allowance-amount").Change("10");
        await page.Find("#allowance-form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("R$ 10,00", page.Markup, StringComparison.Ordinal);
        await page.FindAll("[aria-label='Histórico de abatimentos'] button")[1].ClickAsync(new());
        await page.FindAll("[role=alertdialog] button")[1].ClickAsync(new());
        Assert.Single(page.FindAll("[aria-label='Histórico de abatimentos'] li"));
        var invoices = context.Render<Ags.Finance.Web.Components.Pages.Invoices>();
        invoices.WaitForElement(".invoice-credit");
        var obligations = context.Render<InvoiceObligationsPage>();
        obligations.WaitForElement(".invoice-credit");
        Assert.Contains("Mês corrente", obligations.Markup, StringComparison.Ordinal);
        Assert.Contains("crédito", invoices.Find(".invoice-credit").TextContent, StringComparison.Ordinal);
        Assert.Equal(InvoiceStatus.Open, Assert.Single(await context.Services.GetRequiredService<IInvoiceCatalog>().GetAsync()).Status);
        await page.FindAll("[aria-label='Histórico de abatimentos'] button")[1].ClickAsync(new());
        await page.Find("[role=alertdialog] button").ClickAsync(new());
        Assert.Empty(page.FindAll("[aria-label='Histórico de abatimentos'] li"));
    }
}
