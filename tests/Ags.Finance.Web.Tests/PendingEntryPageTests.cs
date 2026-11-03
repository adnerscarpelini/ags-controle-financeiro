using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>A visão de pendências de Lançamentos (#57, #98), aberta por /pendentes.</summary>
public sealed class PendingEntryPageTests
{
    private sealed record Scenario(PostgresTestDatabase Database, BunitContext Context, IRenderedComponent<MudDialogProvider> Dialogs, Guid Account, Guid Subcategory, Guid Merchant) : IAsyncDisposable
    {
        public IEntryCatalog Entries => Context.Services.GetRequiredService<IEntryCatalog>();

        public IAccountCatalog Accounts => Context.Services.GetRequiredService<IAccountCatalog>();

        public IRenderedComponent<Transactions> Open()
        {
            Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/lancamentos?situacao=pendentes");
            return Context.Render<Transactions>();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    private static List<IElement> Rows(IRenderedComponent<Transactions> page) =>
        page.FindAll(".data-table tbody tr").Where(x => x.QuerySelector("[data-status]") is not null).ToList();

    private static async Task<Scenario> CreateAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var context = new BunitContext().UseAppUi();
        context.Services.AddSingleton<TimeProvider>(new PendingClock()).AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString).AddPreferencesModule(database.ConnectionString).AddReimbursementsModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigratePreferencesModuleAsync();
        await context.Services.MigrateReimbursementsModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Corrente Exemplo", AccountKind.Checking, "green", new Money(1000), new DateOnly(2026, 10, 1)));
        var classes = context.Services.GetRequiredService<IClassificationCatalog>();
        var expense = await classes.SaveCategoryAsync(null, "Despesas Exemplo", CategoryKind.Expense);
        var sub = await classes.SaveSubcategoryAsync(null, expense.ExternalId!.Value, "Serviços Exemplo");
        var merchant = await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Descrição Exemplo", sub.ExternalId);
        context.Render<MudPopoverProvider>();
        return new(database, context, context.Render<MudDialogProvider>(), account.ExternalId!.Value, sub.ExternalId!.Value, merchant.ExternalId!.Value);
    }

    [Fact]
    public async Task PendingViewShowsTimingCombinesFiltersAndRemovesConfirmedOccurrence()
    {
        await using var scenario = await CreateAsync();
        var entries = scenario.Entries;
        for (var day = 1; day <= 3; day++)
        {
            var request = new CreateEntryRequest(new DateOnly(2026, 11, day), scenario.Merchant, scenario.Subcategory, scenario.Account, new Money(50), IsPaid: false);
            if (day == 1) { Assert.True((await entries.CreateAsync(request)).Succeeded); }
            else { Assert.True((await entries.CreateRecurringAsync(new(request))).Succeeded); }
        }

        await scenario.Accounts.DeactivateAccountAsync(scenario.Account);
        var page = scenario.Open();
        page.WaitForAssertion(() => Assert.Equal(3, Rows(page).Count));
        Assert.Equal("true", page.Find("[data-type-filter='pending']").GetAttribute("aria-pressed"));
        Assert.NotNull(page.Find("[data-filter-tag='all-months']"));
        Assert.Contains("Atrasado", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Hoje", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Futuro", page.Markup, StringComparison.Ordinal);
        Assert.Contains("arquivada", page.Markup, StringComparison.Ordinal);

        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        await page.Find("#filter-account").ChangeAsync(scenario.Account.ToString());
        await page.Find("#filter-kind").ChangeAsync(CategoryKind.Expense.ToString());
        await page.Find("#filter-from").ChangeAsync("2026-11-02");
        await page.Find("#filter-to").ChangeAsync("2026-11-02");
        page.WaitForAssertion(() => Assert.Single(Rows(page)));
        var filtered = await entries.QueryPendingAsync(new(FromDate: new DateOnly(2026, 11, 2), ToDate: new DateOnly(2026, 11, 2), AccountExternalId: scenario.Account, Kind: CategoryKind.Expense));
        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal(filtered.Expenses.ToString(), page.Find("[data-stat='expenses'] .stat-value").TextContent);

        await Rows(page)[0].QuerySelector("[data-action=pay]")!.ClickAsync(new MouseEventArgs());
        scenario.Dialogs.WaitForElement("#effective-date");
        Assert.Contains("conta está arquivada", scenario.Dialogs.Markup, StringComparison.Ordinal);
        await scenario.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        Assert.Single(Rows(page));

        await Rows(page)[0].QuerySelector("[data-action=pay]")!.ClickAsync(new MouseEventArgs());
        scenario.Dialogs.WaitForElement("#effective-date");
        await scenario.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(Rows(page)));
        var confirmed = (await entries.GetAsync()).Single(x => x.ExternalId == Assert.Single(filtered.Entries).ExternalId);
        Assert.True(confirmed.IsEffective);
        Assert.Equal(new Money(950), (await entries.GetBalanceAsync(scenario.Account, new DateOnly(2026, 11, 2)))?.EffectiveBalance);
    }

    [Fact]
    public async Task PendingViewOrdersByTheExpectedDateAscending()
    {
        await using var scenario = await CreateAsync();
        await scenario.Entries.CreateAsync(new(new DateOnly(2026, 11, 20), scenario.Merchant, scenario.Subcategory, scenario.Account, new Money(1), IsPaid: false));
        await scenario.Entries.CreateAsync(new(new DateOnly(2026, 11, 5), scenario.Merchant, scenario.Subcategory, scenario.Account, new Money(2), IsPaid: false));

        var page = scenario.Open();

        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
        Assert.Contains("05/11/2026", Rows(page)[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PendingViewUsesThePreferredPageSizeWithoutChangingTheTotals()
    {
        await using var scenario = await CreateAsync();
        Assert.True((await scenario.Context.Services.GetRequiredService<IPreferences>().SaveAsync(AppPreferences.Default with { PageSize = 25 })).Succeeded);
        for (var index = 0; index < 60; index++)
        {
            Assert.True((await scenario.Entries.CreateAsync(new(new DateOnly(2026, 11, 5), scenario.Merchant, scenario.Subcategory, scenario.Account, new Money(1), IsPaid: false))).Succeeded);
        }

        var page = scenario.Open();

        page.WaitForAssertion(() => Assert.Equal("60 lançamentos · Página 1 de 3", page.Find("[data-page-summary]").TextContent));
        Assert.Equal(25, Rows(page).Count);
    }

    private sealed class PendingClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    }
}
