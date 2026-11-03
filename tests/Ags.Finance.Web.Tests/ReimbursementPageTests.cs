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

    [Fact]
    public async Task SelectingAdjustingAndConfirmingReceivesTheItems()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 100m, _anaId, 50m);
        await ExpenseAsync(context, 100m, _biaId, 100m);
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[aria-label=Reembolsos] > li").Count));
        Assert.True(page.Find("#reimbursement-receive").HasAttribute("disabled"));

        await page.Find("#reimbursement-select-all").ChangeAsync(true);
        await page.Find("#reimbursement-receive").ClickAsync(new());
        var dialog = page.Find("[role=dialog]");
        Assert.Contains("Ana Exemplo: R$ 50,00", dialog.TextContent, StringComparison.Ordinal);
        Assert.Contains("Bia Exemplo: R$ 100,00", dialog.TextContent, StringComparison.Ordinal);
        Assert.Equal("2026-10-15", page.Find("#receipt-date").GetAttribute("value"));

        await page.FindAll("[aria-label='Valores recebidos'] input")[0].ChangeAsync("40");
        Assert.Contains("Ana Exemplo: R$ 40,00", page.Find("[aria-label='Receitas que serão criadas']").TextContent, StringComparison.Ordinal);

        await page.FindAll("[role=dialog] button").Single(x => x.TextContent == "Confirmar recebimento").ClickAsync(new());
        Assert.Contains("conta corrente", page.Find("[role=status]").TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(page.FindAll("[role=dialog]"));

        await page.Find("#receipt-account").ChangeAsync(_accountId.ToString());
        await page.FindAll("[role=dialog] button").Single(x => x.TextContent == "Confirmar recebimento").ClickAsync(new());

        page.WaitForAssertion(() => Assert.Contains("Recebido em 15/10/2026", page.Find("[aria-label=Reembolsos]").TextContent, StringComparison.Ordinal));
        Assert.Empty(page.FindAll("[role=dialog]"));
        Assert.Empty(page.FindAll("#reimbursement-select-all"));
        var incomes = (await context.Services.GetRequiredService<IEntryCatalog>().QueryAsync(new(Kind: CategoryKind.Income))).Entries;
        Assert.Equal([new Money(40m), new Money(100m)], incomes.OrderBy(x => x.Amount.Amount).Select(x => x.Amount));
    }

    [Fact]
    public async Task OnlyCheckingAccountsAreOfferedAndCancellingChangesNothing()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 100m, _anaId, 50m);
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = (await accounts.GetBanksAsync())[0].ExternalId;
        await accounts.SaveAccountAsync(new(null, bank, "Poupança Exemplo", AccountKind.Savings, "green"));
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[aria-label=Reembolsos] > li")));

        await page.Find("[aria-label=Reembolsos] input[type=checkbox]").ChangeAsync(true);
        await page.Find("#reimbursement-receive").ClickAsync(new());
        Assert.DoesNotContain("Poupança", page.Find("#receipt-account").TextContent, StringComparison.Ordinal);

        await page.FindAll("[role=dialog] button").Single(x => x.TextContent == "Cancelar").ClickAsync(new());
        Assert.Empty(page.FindAll("[role=dialog]"));
        Assert.Empty((await context.Services.GetRequiredService<IEntryCatalog>().QueryAsync(new(Kind: CategoryKind.Income))).Entries);
    }

    [Fact]
    public async Task UndoShowsAllItemsOfTheReceiptAndCancellingChangesNothing()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 100m, _anaId, 100m);
        await ExpenseAsync(context, 50m, _anaId, 100m, new DateOnly(2026, 9, 5));
        var entries = context.Services.GetRequiredService<IEntryCatalog>();
        var rows = await entries.GetAsync();
        var result = await context.Services.GetRequiredService<IReimbursementReceipts>().ReceiveAsync(new(new DateOnly(2026, 10, 15), _accountId, rows.Select(x => new ReceiveReimbursementItem(x.ExternalId, new Money(Math.Abs(x.Amount.Amount)), new Money(Math.Abs(x.Amount.Amount)))).ToList()));
        Assert.True(result.Succeeded, result.Error);
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[aria-label=Reembolsos] > li")));

        await page.FindAll("[aria-label=Reembolsos] button").Single(x => x.TextContent == "Desfazer recebimento").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[aria-label='Itens do recebimento'] > li").Count));
        Assert.Contains("Conta Exemplo", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal);
        Assert.Contains("R$ 150,00", page.Find("[role=alertdialog]").TextContent, StringComparison.Ordinal);

        await page.FindAll("[role=alertdialog] button").Single(x => x.TextContent == "Cancelar").ClickAsync(new());
        Assert.Empty(page.FindAll("[role=alertdialog]"));
        Assert.Single((await entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);

        await page.FindAll("[aria-label=Reembolsos] button").Single(x => x.TextContent == "Desfazer recebimento").ClickAsync(new());
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[role=alertdialog]")));
        await page.FindAll("[role=alertdialog] button").Single(x => x.TextContent == "Confirmar").ClickAsync(new());

        page.WaitForAssertion(() => Assert.Contains("Pendente", page.Find("[aria-label=Reembolsos]").TextContent, StringComparison.Ordinal));
        Assert.Empty((await entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }
}
