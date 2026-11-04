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
    private static readonly ReferenceMonth s_month = new(2026, 11);
    private static async Task<Guid> SetUpAsync(BunitContext context, PostgresTestDatabase database)
    {
        context.Services.AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero)))
            .AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString)
            .AddInvoicesModule(database.ConnectionString).AddPreferencesModule(database.ConnectionString).AddReportsModule();
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        await context.Services.MigratePreferencesModuleAsync();
        var category = await context.Services.GetRequiredService<IClassificationCatalog>().SaveCategoryAsync(null, "Alimentação", CategoryKind.Expense);
        return category.ExternalId!.Value;
    }
    private static async Task ClickAsync(IRenderedComponent<MonthlyPlanning> page, string text)
        => await page.FindAll("button").Single(x => x.TextContent.Trim() == text).ClickAsync(new());
    private static IRenderedComponent<MonthlyPlanning> Render(BunitContext context)
    {
        var page = context.Render<MonthlyPlanning>();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("#define-monthly-plan, .monthly-plan-actions")));
        return page;
    }

    [Fact]
    public async Task EmptyMonthOffersCreationAndCopyAndMissingPreviousMonthDoesNotCreateAnything()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        await SetUpAsync(context, database);
        var page = Render(context);
        Assert.Contains("Novembro de 2026", page.Markup, StringComparison.Ordinal);
        await page.Find("#copy-monthly-plan").ClickAsync(new());
        Assert.Contains("O mês anterior não tem planejamento.", page.Find("[role=status]").TextContent);
        Assert.Null(await context.Services.GetRequiredService<IMonthlyPlanCatalog>().GetAsync(s_month));
        await ClickAsync(page, "Próximo mês");
        Assert.Contains("Dezembro de 2026", page.Markup, StringComparison.Ordinal);
        await ClickAsync(page, "Mês anterior");
        Assert.Contains("Novembro de 2026", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WizardCalculatesPreviewValidatesAndSavesTheThreeSteps()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        var category = await SetUpAsync(context, database);
        var page = Render(context);
        await page.Find("#define-monthly-plan").ClickAsync(new());
        Assert.Equal("20", page.Find("#plan-saving").GetAttribute("value"));
        await page.Find("#plan-income").ChangeAsync(new() { Value = "1000" });
        page.WaitForAssertion(() =>
        {
            Assert.Contains("R$ 800,00", page.Find(".monthly-plan-preview").TextContent);
            Assert.Contains("R$ 200,00", page.Find(".monthly-plan-preview").TextContent);
        });
        await page.Find("#plan-saving").ChangeAsync(new() { Value = "101" });
        await page.Find("#plan-next").ClickAsync(new());
        Assert.NotEmpty(page.FindAll("[role=alert]"));
        Assert.NotEmpty(page.FindAll("#plan-income"));
        await page.Find("#plan-saving").ChangeAsync(new() { Value = "20" });
        await page.Find("#plan-next").ClickAsync(new());
        await page.Find("input[type=checkbox]").ChangeAsync(new() { Value = true });
        await page.Find($"#plan-target-{category}").ChangeAsync(new() { Value = "-1" });
        await page.Find("#plan-next").ClickAsync(new());
        Assert.NotEmpty(page.FindAll("[role=alert]"));
        await page.Find($"#plan-target-{category}").ChangeAsync(new() { Value = "200" });
        page.WaitForAssertion(() => Assert.Contains("R$ 600,00", page.Find(".monthly-plan-preview").TextContent));
        await page.Find("#plan-next").ClickAsync(new());
        Assert.Equal("InvoiceDate", page.Find("#plan-card-view").GetAttribute("value"));
        await page.Find("#plan-save").ClickAsync(new());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".monthly-plan-actions")));
        var saved = (await context.Services.GetRequiredService<IMonthlyPlanCatalog>().GetAsync(s_month))!;
        Assert.Equal((new Money(800), new Money(600)), (saved.SpendingBudget, saved.UnclassifiedBudget));
        Assert.Equal(new Money(200), Assert.Single(saved.Categories).Target);
    }

    [Fact]
    public async Task CopyKeepsSettingsAndCategoryActionsAndPlanDeletionRequireTheExplicitActions()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        var category = await SetUpAsync(context, database);
        var plans = context.Services.GetRequiredService<IMonthlyPlanCatalog>();
        Assert.True((await plans.CreateAsync(new(s_month.AddMonths(-1), new Money(1000), 30m, MonthlyPlanCardView.PurchaseDate, [new(category, new Money(200))]))).Succeeded);
        var page = Render(context);
        await page.Find("#copy-monthly-plan").ClickAsync(new());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll(".monthly-plan-actions")));
        Assert.Equal(MonthlyPlanCardView.PurchaseDate, (await plans.GetAsync(s_month))!.CardView);
        await page.Find("#monthly-plan-card-view").ChangeAsync(new() { Value = "InvoiceDate" });
        Assert.Equal(MonthlyPlanCardView.InvoiceDate, (await plans.GetAsync(s_month))!.CardView);
        await ClickAsync(page, "Editar");
        await page.Find("#monthly-plan-target").ChangeAsync(new() { Value = "900" });
        await ClickAsync(page, "Salvar meta");
        Assert.Equal(new Money(-200), (await plans.GetAsync(s_month))!.UnclassifiedBudget);
        Assert.Equal(new Money(200), Assert.Single((await plans.GetAsync(s_month.AddMonths(-1)))!.Categories).Target);
        await page.Find("#monthly-plan-search").InputAsync(new() { Value = "inexistente" });
        Assert.Empty(page.FindAll("tbody tr"));
        await page.Find("#monthly-plan-search").InputAsync(new() { Value = "Alimentação" });
        Assert.Single(page.FindAll("tbody tr"));
        await ClickAsync(page, "Remover");
        await ClickAsync(page, "Cancelar");
        Assert.Single((await plans.GetAsync(s_month))!.Categories);
        await ClickAsync(page, "Remover");
        await ClickAsync(page, "Confirmar exclusão");
        Assert.Empty((await plans.GetAsync(s_month))!.Categories);
        await ClickAsync(page, "Novo orçamento de categoria");
        await page.Find("#monthly-plan-category").ChangeAsync(new() { Value = category.ToString() });
        await page.Find("#monthly-plan-target").ChangeAsync(new() { Value = "200" });
        await ClickAsync(page, "Salvar meta");
        Assert.Single((await plans.GetAsync(s_month))!.Categories);
        await ClickAsync(page, "Excluir planejamento");
        await ClickAsync(page, "Cancelar");
        Assert.NotNull(await plans.GetAsync(s_month));
        await ClickAsync(page, "Excluir planejamento");
        await ClickAsync(page, "Confirmar exclusão");
        Assert.Null(await plans.GetAsync(s_month));
        Assert.NotNull(await plans.GetAsync(s_month.AddMonths(-1)));
    }

    [Fact]
    public async Task ExceededCategoryAndRealIncomeCardsShowTheAcceptanceValues()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var context = new BunitContext();
        var category = await SetUpAsync(context, database);
        var classifications = context.Services.GetRequiredService<IClassificationCatalog>();
        var subcategory = (await classifications.SaveSubcategoryAsync(null, category, "Compras")).ExternalId!.Value;
        var incomeCategory = (await classifications.SaveCategoryAsync(null, "Trabalho", CategoryKind.Income)).ExternalId!.Value;
        var income = (await classifications.SaveSubcategoryAsync(null, incomeCategory, "Salário")).ExternalId!.Value;
        var merchant = (await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Loja Exemplo", subcategory)).ExternalId!.Value;
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = (await accounts.CreateBankAsync("Banco Exemplo")).ExternalId!.Value;
        var account = (await accounts.SaveAccountAsync(new(null, bank, "Conta Exemplo", AccountKind.Checking, "green"))).ExternalId!.Value;
        var entries = context.Services.GetRequiredService<IEntryCatalog>();
        foreach (var (amount, paid, classification) in new[] { (296.66m, true, subcategory), (50m, false, subcategory), (200m, true, income), (900m, false, income) })
        {
            Assert.True((await entries.CreateAsync(new(new DateOnly(2026, 11, 2), merchant, classification, account, new Money(amount), IsPaid: paid))).Succeeded);
        }
        Assert.True((await context.Services.GetRequiredService<IMonthlyPlanCatalog>().CreateAsync(new(s_month, new Money(1000), Categories: [new(category, new Money(200))]))).Succeeded);
        var page = Render(context);
        var row = page.FindAll("tbody tr")[0];
        Assert.Contains("R$ 296,66", row.TextContent);
        Assert.Contains("R$ 50,00", row.TextContent);
        Assert.Contains("R$ 346,66", row.TextContent);
        Assert.Contains("Excederam R$ 146,66", row.TextContent);
        Assert.Contains("monthly-plan-exceeded", row.ClassList);
        Assert.Contains("R$ 200,00", page.Find("aside").TextContent);
        Assert.Contains(new Money(-600).ToString(), page.Find("aside").TextContent);
        Assert.Contains("-300,00%", page.Find("aside").TextContent);
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
