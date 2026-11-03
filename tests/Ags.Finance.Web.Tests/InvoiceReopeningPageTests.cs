using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class InvoiceReopeningPageTests
{
    [Fact]
    public async Task ReopeningListsThePaymentsSupportsCancelAndShowsTheUndoneHistory()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddSingleton<TimeProvider>(new ReopeningClock()).AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString).AddInvoicesModule(database.ConnectionString).AddPreferencesModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        await context.Services.MigratePreferencesModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = (await accounts.CreateBankAsync("Banco Exemplo")).ExternalId!.Value;
        var account = (await accounts.SaveAccountAsync(new(null, bank, "Conta Exemplo", AccountKind.Checking, "green", new Money(1000), new DateOnly(2026, 9, 1)))).ExternalId!.Value;
        var card = (await accounts.SaveCreditCardAsync(new(null, account, "Cartão Exemplo", CardBrand.Visa, new Money(1000), 3, 10, true, "green"))).ExternalId!.Value;
        var classes = context.Services.GetRequiredService<IClassificationCatalog>();
        var category = (await classes.SaveCategoryAsync(null, "Casa", CategoryKind.Expense)).ExternalId!.Value;
        var subcategory = (await classes.SaveSubcategoryAsync(null, category, "Serviços")).ExternalId!.Value;
        var merchant = (await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Loja Exemplo", subcategory)).ExternalId!.Value;
        var entries = context.Services.GetRequiredService<IEntryCatalog>();
        Assert.True((await entries.CreateAsync(new(new DateOnly(2026, 9, 14), merchant, subcategory, account, new Money(66.66m), CardExternalId: card))).Succeeded);
        var month = new ReferenceMonth(2026, 10);
        await context.Services.GetRequiredService<IInvoiceLifecycle>().CloseAsync(card, month);
        Assert.True((await context.Services.GetRequiredService<IInvoicePaymentCatalog>().ConfirmPaymentAsync(new(Guid.CreateVersion7(), card, month, account, new DateOnly(2026, 9, 1), new Money(66.66m), true))).Succeeded);
        var page = context.Render<Ags.Finance.Web.Components.Pages.Invoices>();
        page.WaitForAssertion(() => Assert.Contains("Reabrir fatura", page.Markup, StringComparison.Ordinal));
        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Reabrir fatura").ClickAsync(new());
        Assert.Contains("Conta Exemplo", page.Find("[aria-label='Pagamentos a desfazer']").TextContent, StringComparison.Ordinal);
        Assert.Contains("66,66", page.Find("[aria-label='Pagamentos a desfazer']").TextContent, StringComparison.Ordinal);
        await page.FindAll("[role=alertdialog] button").Single(x => x.TextContent == "Cancelar").ClickAsync(new());
        Assert.Equal(new Money(933.34m), (await entries.GetBalanceAsync(account, new DateOnly(2026, 9, 1)))?.EffectiveBalance);
        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Reabrir fatura").ClickAsync(new());
        await page.FindAll("[role=alertdialog] button").Single(x => x.TextContent == "Confirmar").ClickAsync(new());
        Assert.Equal(new Money(1000), (await entries.GetBalanceAsync(account, new DateOnly(2026, 9, 1)))?.EffectiveBalance);
        await page.FindAll(".catalog-list button").Single(x => x.TextContent == "Ver composição").ClickAsync(new());
        Assert.Contains("Pagamento desfeito", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("irreversível", page.Markup, StringComparison.Ordinal);
    }

    private sealed class ReopeningClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
