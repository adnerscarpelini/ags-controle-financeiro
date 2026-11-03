using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Reports;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class PlanningPageTests
{
    /// <summary>Hoje é 03/11/2026. Com dados, há um lançamento de R$ 100,00 e uma recorrente de R$ 50,00 em "Casa", além de uma receita em "Trabalho".</summary>
    private static async Task SetUpAsync(BunitContext context, PostgresTestDatabase database, bool withData = true)
    {
        context.Services.AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)))
            .AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString)
            .AddInvoicesModule(database.ConnectionString).AddPreferencesModule(database.ConnectionString).AddReportsModule();
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        await context.Services.MigratePreferencesModuleAsync();
        if (!withData) { return; }

        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        var classifications = context.Services.GetRequiredService<IClassificationCatalog>();
        var home = await classifications.SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
        var services = await classifications.SaveSubcategoryAsync(null, home.ExternalId!.Value, "Assinaturas");
        var work = await classifications.SaveCategoryAsync(null, "Trabalho", CategoryKind.Income);
        var salary = await classifications.SaveSubcategoryAsync(null, work.ExternalId!.Value, "Salário");
        var merchant = await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Serviço Exemplo", services.ExternalId!.Value);
        var entries = context.Services.GetRequiredService<IEntryCatalog>();
        CreateEntryRequest Request(decimal amount, Guid subcategory, DateOnly date) => new(date, merchant.ExternalId!.Value, subcategory, account.ExternalId!.Value, new Money(amount));
        Assert.True((await entries.CreateAsync(Request(100, services.ExternalId!.Value, new DateOnly(2026, 11, 1)))).Succeeded);
        Assert.True((await entries.CreateAsync(Request(900, salary.ExternalId!.Value, new DateOnly(2026, 11, 1)))).Succeeded);
        Assert.True((await entries.CreateRecurringAsync(new(Request(50, services.ExternalId!.Value, new DateOnly(2026, 12, 20)) with { IsPaid = false }))).Succeeded);
    }

    private static IRenderedComponent<Planning> Render(BunitContext context)
    {
        var page = context.Render<Planning>();
        page.WaitForAssertion(() => Assert.DoesNotContain("Consultando", page.Markup, StringComparison.Ordinal));
        return page;
    }

    [Fact]
    public async Task ShowsTheNineteenMonthsWithTheCurrentOneHighlighted()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);

        var headers = page.FindAll("thead th.numeric");
        Assert.Equal(20, headers.Count);
        Assert.Equal("mai/26", headers[0].TextContent);
        Assert.Equal("nov/27", headers[18].TextContent);
        var current = page.Find("thead th[aria-current='date']");
        Assert.Equal("nov/26", current.TextContent);
        Assert.Contains("planning-current", current.ClassList);
    }

    [Fact]
    public async Task ArrowsMoveThePeriodOneMonth()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);

        await page.FindAll("button").First(x => x.TextContent == "Próximo mês").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Equal("jun/26", page.FindAll("thead th.numeric")[0].TextContent));
        Assert.Equal(20, page.FindAll("thead th.numeric").Count);

        await page.FindAll("button").First(x => x.TextContent == "Mês anterior").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Equal("mai/26", page.FindAll("thead th.numeric")[0].TextContent));
    }

    [Fact]
    public async Task CategoriesStartClosedAndOpenIntoSubcategoriesWithoutChangingTheTotals()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);
        var totals = page.Find("tbody tr").TextContent;

        Assert.Empty(page.FindAll(".planning-subcategory"));
        var toggle = page.FindAll(".planning-toggle").First(x => x.TextContent.Contains("Casa", StringComparison.Ordinal));
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));

        await toggle.ClickAsync(new());
        var row = Assert.Single(page.FindAll(".planning-subcategory"));
        Assert.Contains("Assinaturas", row.TextContent, StringComparison.Ordinal);
        Assert.Equal(totals, page.Find("tbody tr").TextContent);

        await page.FindAll(".planning-toggle").First(x => x.TextContent.Contains("Casa", StringComparison.Ordinal)).ClickAsync(new());
        Assert.Empty(page.FindAll(".planning-subcategory"));
        Assert.Equal(totals, page.Find("tbody tr").TextContent);
    }

    [Fact]
    public async Task ExpandAllAndCollapseAllAffectEveryCategory()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);

        await page.FindAll("button").First(x => x.TextContent == "Expandir tudo").ClickAsync(new());
        Assert.Equal(2, page.FindAll(".planning-subcategory").Count);
        await page.FindAll("button").First(x => x.TextContent == "Recolher tudo").ClickAsync(new());
        Assert.Empty(page.FindAll(".planning-subcategory"));
    }

    [Fact]
    public async Task ForecastCellIsMarkedAndItsDetailUsesTheFormatTheValueMaskRecognizes()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);

        const string Detail = "Despesas em dez/26: lançado R$ 0,00 + previsto R$ 50,00 = R$ 50,00";
        var marked = page.FindAll(".planning-forecast").First(x => x.GetAttribute("title") == Detail);
        Assert.Equal(Detail, marked.GetAttribute("aria-label"));
        Assert.Equal("button", marked.GetAttribute("type"));
        Assert.Equal("-R$ 50,00", marked.TextContent);

        await marked.ClickAsync(new());
        Assert.Equal(Detail, page.Find("[data-testid='planning-detail']").TextContent);
    }

    [Fact]
    public async Task ALaunchedValueIsNotMarked()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);

        Assert.DoesNotContain(page.FindAll(".planning-forecast"), x => x.GetAttribute("title")!.Contains("nov/26", StringComparison.Ordinal));
        Assert.Contains("R$ 900,00", page.Find("tbody tr").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccumulatedBalanceRowSaysItIsNotAnAccountBalance()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);

        var row = page.FindAll("tbody tr").First(x => x.TextContent.Contains("Saldo acumulado", StringComparison.Ordinal));
        Assert.Contains("não é saldo de conta", row.TextContent, StringComparison.Ordinal);
        Assert.Equal("-", row.QuerySelectorAll("td")[0].TextContent);
        Assert.Equal("R$ 800,00", row.QuerySelectorAll("td")[6].TextContent);
        Assert.Equal("R$ 750,00", row.QuerySelectorAll("td")[7].TextContent);
    }

    [Fact]
    public async Task AnEmptyPeriodShowsAMessage()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database, withData: false);
        var page = Render(context);

        Assert.Contains("Nenhuma receita ou despesa neste período.", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("table"));
        Assert.Empty(page.FindAll("[role='alert']"));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
