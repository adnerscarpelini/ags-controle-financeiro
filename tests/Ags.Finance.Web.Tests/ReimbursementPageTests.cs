using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class ReimbursementPageTests : IAsyncLifetime
{
    private PostgresTestDatabase? _database;
    private Guid _accountId;
    private Guid _subcategoryId;
    private Guid _merchantId;
    private Guid _anaId;
    private Guid _biaId;

    public async Task InitializeAsync() => _database = await PostgresTestDatabase.CreateAsync();

    public async Task DisposeAsync()
    {
        if (_database is not null) { await _database.DisposeAsync(); }
    }

    private async Task<BunitContext> ContextAsync()
    {
        var context = new BunitContext();
        context.Services.AddSingleton<TimeProvider>(new FixedClock()).AddAccountsModule(_database!.ConnectionString).AddTransactionsModule(_database.ConnectionString).AddReimbursementsModule(_database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateReimbursementsModuleAsync();
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        _accountId = (await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"))).ExternalId!.Value;
        var classifications = context.Services.GetRequiredService<IClassificationCatalog>();
        var category = await classifications.SaveCategoryAsync(null, "Casa", CategoryKind.Expense);
        _subcategoryId = (await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, "Internet")).ExternalId!.Value;
        _merchantId = (await context.Services.GetRequiredService<IMerchantCatalog>().SaveAsync(null, "Provedor Exemplo", _subcategoryId)).ExternalId!.Value;
        var people = context.Services.GetRequiredService<IPersonCatalog>();
        _anaId = (await people.SaveAsync(null, "Ana Exemplo", 50m)).ExternalId!.Value;
        _biaId = (await people.SaveAsync(null, "Bia Exemplo", 100m)).ExternalId!.Value;
        return context;
    }

    private async Task ExpenseAsync(BunitContext context, decimal amount, Guid person, decimal percent, DateOnly? date = null)
        => Assert.True((await context.Services.GetRequiredService<IEntryCatalog>().CreateAsync(new(date ?? new DateOnly(2026, 10, 5), _merchantId, _subcategoryId, _accountId, new Money(amount), Reimbursement: new(person, percent)))).Succeeded);

    [Fact]
    public async Task ListsTheCurrentMonthWithTotalsAndFilters()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 142.90m, _anaId, 50m);
        await ExpenseAsync(context, 100m, _biaId, 100m);
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[aria-label=Reembolsos] > li").Count));

        Assert.Equal("2026-10", page.Find("#reimbursement-month").GetAttribute("value"));
        var text = page.Find("[aria-label=Reembolsos]").TextContent;
        Assert.Contains("R$ 142,90 × 50%", text, StringComparison.Ordinal);
        Assert.Contains("R$ 71,45", text, StringComparison.Ordinal);
        Assert.Contains("Pendente", text, StringComparison.Ordinal);
        Assert.Contains("R$ 171,45", page.Find("[aria-label='Totais dos reembolsos']").TextContent, StringComparison.Ordinal);

        await page.Find("#reimbursement-person").ChangeAsync(_biaId.ToString());
        await page.Find("#reimbursement-filters").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[aria-label=Reembolsos] > li")));
        Assert.Contains("Bia Exemplo", page.Find("[aria-label=Reembolsos]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WarnsAboutPreviousPendingsAndAllMonthsListsThem()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 100m, _anaId, 50m, new DateOnly(2026, 9, 5));
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Contains("R$ 50,00", page.Find("[role=note]").TextContent, StringComparison.Ordinal));
        Assert.Contains("Nenhum reembolso", page.Markup, StringComparison.Ordinal);

        await page.Find("#reimbursement-all-months").ChangeAsync(true);
        await page.Find("#reimbursement-filters").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[aria-label=Reembolsos] > li")));
        Assert.Empty(page.FindAll("[role=note]"));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }
}
