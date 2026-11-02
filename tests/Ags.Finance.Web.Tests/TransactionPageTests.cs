using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class TransactionPageTests
{
    [Fact]
    public async Task QuickEntryUsesMerchantDefaultPersistsManualMonthAndKeepsDateForNextEntry()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddSingleton<TimeProvider>(new PreviewClock()).AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString).AddInvoicesModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        var card = await accounts.SaveCreditCardAsync(new(null, account.ExternalId!.Value, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"));
        var classifications = context.Services.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Feira");
        await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Loja Exemplo", subcategory.ExternalId);
        var page = context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Contains("Feira", page.Find("#entry-subcategory").TextContent, StringComparison.Ordinal));
        await page.Find("#entry-date").ChangeAsync("2026-10-05");
        await page.Find("#entry-merchant").InputAsync("Loja Exemplo");
        Assert.Equal(subcategory.ExternalId!.Value.ToString(), page.Find("#entry-subcategory").GetAttribute("value"));
        page.Find("#entry-account").Change(account.ExternalId!.Value.ToString());
        page.Find("#entry-amount").Change("150");
        page.Find("#entry-month").Change("2026-11");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        var entry = Assert.Single(await context.Services.GetRequiredService<IEntryCatalog>().GetAsync());
        Assert.Equal(new Money(-150), entry.Amount);
        Assert.Equal(new AccountingMonth(2026, 11), entry.Month);
        Assert.Equal("2026-10-05", page.Find("#entry-date").GetAttribute("value"));
        Assert.Equal(string.Empty, page.Find("#entry-merchant").GetAttribute("value"));
        await page.Find("#entry-card").ChangeAsync(card.ExternalId!.Value.ToString());
        Assert.Equal("2026-11", page.Find("#entry-month").GetAttribute("value"));
        await page.Find("#entry-date").ChangeAsync("2026-10-02");
        Assert.Equal("2026-10", page.Find("#entry-month").GetAttribute("value"));
        await page.Find("#entry-merchant").InputAsync("Loja Exemplo");
        page.Find("#entry-amount").Change("100");
        page.Find("#entry-month").Change("2026-11");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        var purchase = (await context.Services.GetRequiredService<IEntryCatalog>().GetAsync()).Single(x => x.CardExternalId is not null);
        Assert.Equal(card.ExternalId, purchase.CardExternalId);
        Assert.Equal(new DateOnly(2026, 10, 2), purchase.Date);
        Assert.Equal(new AccountingMonth(2026, 11), purchase.Month);
        var invoices = context.Render<Ags.Finance.Web.Components.Pages.Invoices>();
        invoices.WaitForAssertion(() => Assert.Single(invoices.FindAll(".catalog-list li")));
        invoices.Find(".catalog-list button").Click();
        Assert.Contains("Loja Exemplo", invoices.Find("[aria-label='Composição da fatura']").TextContent, StringComparison.Ordinal);
        Assert.Single(invoices.FindAll("[aria-label='Composição da fatura'] li"));
    }

    private sealed class PreviewClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
