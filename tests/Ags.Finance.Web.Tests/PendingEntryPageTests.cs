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

public sealed class PendingEntryPageTests
{
    [Fact]
    public async Task PendingPageShowsTimingCombinesFiltersAndRemovesConfirmedOccurrence()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        context.Services.AddSingleton<TimeProvider>(new PendingClock()).AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Corrente Exemplo", AccountKind.Checking, "green", new Money(1000), new DateOnly(2026, 10, 1)));
        var classes = context.Services.GetRequiredService<IClassificationCatalog>();
        var expense = await classes.SaveCategoryAsync(null, "Despesas Exemplo", CategoryKind.Expense);
        var sub = await classes.SaveSubcategoryAsync(null, expense.ExternalId!.Value, "Serviços Exemplo");
        var merchant = await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Descrição Exemplo", sub.ExternalId);
        var entries = context.Services.GetRequiredService<IEntryCatalog>();
        for (var day = 1; day <= 3; day++) { await entries.CreateRecurringAsync(new(new(new DateOnly(2026, 11, day), merchant.ExternalId!.Value, sub.ExternalId!.Value, account.ExternalId!.Value, new Money(50)))); }
        await accounts.DeactivateAccountAsync(account.ExternalId!.Value);
        var page = context.Render<PendingEntries>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("[aria-label='Lançamentos pendentes'] li").Count));
        Assert.Contains("Atrasado", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Hoje", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Futuro", page.Markup, StringComparison.Ordinal);
        Assert.Contains("arquivada", page.Markup, StringComparison.Ordinal);
        page.Find("#pending-account").Change(account.ExternalId.Value.ToString());
        page.Find("#pending-kind").Change(CategoryKind.Expense.ToString());
        page.Find("#pending-from").Change("2026-11-02");
        page.Find("#pending-to").Change("2026-11-02");
        await page.Find("#pending-filters").SubmitAsync(EventArgs.Empty);
        Assert.Single(page.FindAll("[aria-label='Lançamentos pendentes'] li"));
        var filtered = await entries.QueryPendingAsync(new(FromDate: new DateOnly(2026, 11, 2), ToDate: new DateOnly(2026, 11, 2), AccountExternalId: account.ExternalId, Kind: CategoryKind.Expense));
        Assert.Equal(1, filtered.TotalCount);
        Assert.Contains(filtered.Expenses.ToString(), page.Find("[aria-label='Totais das pendências']").TextContent, StringComparison.Ordinal);
        await page.Find("[aria-label='Lançamentos pendentes'] button").ClickAsync(new());
        page.WaitForElement("#effective-date");
        Assert.Contains("conta está arquivada", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal);
        await page.FindAll("[role=alertdialog] button")[1].ClickAsync(new());
        Assert.Single(page.FindAll("[aria-label='Lançamentos pendentes'] li"));
        await page.Find("[aria-label='Lançamentos pendentes'] button").ClickAsync(new());
        page.WaitForElement("#effective-date");
        await page.Find("[role=alertdialog] button").ClickAsync(new());
        Assert.Empty(page.FindAll("[aria-label='Lançamentos pendentes'] li"));
        var confirmed = (await entries.GetAsync()).Single(x => x.ExternalId == Assert.Single(filtered.Entries).ExternalId);
        Assert.True(confirmed.IsEffective);
        Assert.Equal(new Money(950), (await entries.GetBalanceAsync(account.ExternalId.Value, new DateOnly(2026, 11, 2)))?.EffectiveBalance);
    }

    private sealed class PendingClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    }
}
