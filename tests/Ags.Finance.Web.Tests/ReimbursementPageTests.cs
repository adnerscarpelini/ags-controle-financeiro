using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Pages;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

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
        var context = new BunitContext().UseAppUi();
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

    private static List<IElement> Rows(IRenderedComponent<Reimbursements> page) =>
        page.FindAll("[aria-label=Reembolsos] tbody tr").Where(x => x.QuerySelector("td.col-select") is not null).ToList();

    [Fact]
    public async Task ListsTheItemsWithTotalsAndFiltersByPerson()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 142.90m, _anaId, 50m);
        await ExpenseAsync(context, 100m, _biaId, 100m);
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));

        Assert.Equal("Outubro de 2026", page.Find("[data-month-label]").TextContent);
        var text = page.Find("[aria-label=Reembolsos]").TextContent;
        var headers = page.FindAll("[aria-label=Reembolsos] thead th").Select(x => x.TextContent.Trim()).ToList();
        Assert.Equal(["Despesa", "Pessoa", "Valor da conta", "Percentual", "A receber"], headers.Skip(1).Take(5));
        Assert.Contains(page.FindAll("[data-expense-amount]"), x => x.TextContent == "R$ 142,90");
        Assert.DoesNotContain("×", text, StringComparison.Ordinal);
        Assert.Contains("R$ 71,45", text, StringComparison.Ordinal);
        Assert.Contains("Pendente", text, StringComparison.Ordinal);
        Assert.Equal("R$ 171,45", page.Find("[data-stat='expected'] .stat-value").TextContent);
        Assert.Equal(2, page.FindAll("[data-person-total]").Count);
        Assert.Empty(page.FindAll("#reimbursement-filters"));

        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        await page.Find("#reimbursement-person").ChangeAsync(_biaId.ToString());
        page.WaitForAssertion(() => Assert.Single(Rows(page)));
        Assert.Contains("Bia Exemplo", page.Find("[aria-label=Reembolsos]").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMonthPickerChangesTheList()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 100m, _anaId, 50m);
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Single(Rows(page)));

        await page.Find("[data-month-previous]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(Rows(page)));
        Assert.Contains("Nenhum reembolso", page.Find(".empty-state").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WarnsAboutPreviousPendingsAndAllMonthsListsThem()
    {
        await using var context = await ContextAsync();
        await ExpenseAsync(context, 100m, _anaId, 50m, new DateOnly(2026, 9, 5));
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Contains("R$ 50,00", page.Find("[role=note]").TextContent, StringComparison.Ordinal));
        Assert.Contains("Nenhum reembolso", page.Markup, StringComparison.Ordinal);

        await page.Find("[data-toggle-filters]").ClickAsync(new MouseEventArgs());
        await page.Find("#reimbursement-all-months").ChangeAsync(true);
        page.WaitForAssertion(() => Assert.Single(Rows(page)));
        Assert.Empty(page.FindAll("[role=note]"));
    }

    [Fact]
    public async Task SelectingAdjustingAndConfirmingReceivesTheItems()
    {
        await using var context = await ContextAsync();
        var dialogs = context.Render<MudDialogProvider>();
        context.Render<MudPopoverProvider>();
        await ExpenseAsync(context, 100m, _anaId, 50m);
        await ExpenseAsync(context, 100m, _biaId, 100m);
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
        Assert.True(page.Find("#reimbursement-receive").HasAttribute("disabled"));

        await page.Find("#reimbursement-select-all").ChangeAsync(true);
        Assert.Contains("Registrar recebimento (2)", page.Find("#reimbursement-receive").TextContent, StringComparison.Ordinal);
        await page.Find("#reimbursement-receive").ClickAsync(new MouseEventArgs());
        dialogs.WaitForAssertion(() => Assert.Contains("Ana Exemplo: R$ 50,00", dialogs.Find("[aria-label='Receitas que serão criadas']").TextContent, StringComparison.Ordinal));
        Assert.Contains("Bia Exemplo: R$ 100,00", dialogs.Find("[aria-label='Receitas que serão criadas']").TextContent, StringComparison.Ordinal);
        Assert.Equal("2026-10-15", dialogs.Find("#receipt-date").GetAttribute("value"));

        await dialogs.FindAll("[aria-label='Valores recebidos'] input")[0].ChangeAsync("40");
        Assert.Contains("Ana Exemplo: R$ 40,00", dialogs.Find("[aria-label='Receitas que serão criadas']").TextContent, StringComparison.Ordinal);

        await dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());
        Assert.Contains("conta corrente", dialogs.Find("[data-entry-error]").TextContent, StringComparison.Ordinal);

        await dialogs.Find("#receipt-account").ChangeAsync(_accountId.ToString());
        await dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Contains("Recebido em 15/10/2026", page.Find("[aria-label=Reembolsos]").TextContent, StringComparison.Ordinal));
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("#receipt-date")));
        Assert.Empty(page.FindAll("#reimbursement-select-all"));
        var incomes = (await context.Services.GetRequiredService<IEntryCatalog>().QueryAsync(new(Kind: CategoryKind.Income))).Entries;
        Assert.Equal([new Money(40m), new Money(100m)], incomes.OrderBy(x => x.Amount.Amount).Select(x => x.Amount));
    }

    [Fact]
    public async Task OnlyCheckingAccountsAreOfferedAndCancellingChangesNothing()
    {
        await using var context = await ContextAsync();
        var dialogs = context.Render<MudDialogProvider>();
        context.Render<MudPopoverProvider>();
        await ExpenseAsync(context, 100m, _anaId, 50m);
        var accounts = context.Services.GetRequiredService<IAccountCatalog>();
        var bank = (await accounts.GetBanksAsync())[0].ExternalId;
        await accounts.SaveAccountAsync(new(null, bank, "Poupança Exemplo", AccountKind.Savings, "green"));
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Single(Rows(page)));

        await Rows(page)[0].QuerySelector("input[type=checkbox]")!.ChangeAsync(true);
        await page.Find("#reimbursement-receive").ClickAsync(new MouseEventArgs());
        dialogs.WaitForAssertion(() => Assert.Contains("Conta Exemplo", dialogs.Find("#receipt-account").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain("Poupança", dialogs.Find("#receipt-account").TextContent, StringComparison.Ordinal);

        await dialogs.Find("[data-dialog-cancel]").ClickAsync(new MouseEventArgs());
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("#receipt-account")));
        Assert.Empty((await context.Services.GetRequiredService<IEntryCatalog>().QueryAsync(new(Kind: CategoryKind.Income))).Entries);
    }

    [Fact]
    public async Task UndoShowsAllItemsOfTheReceiptAndCancellingChangesNothing()
    {
        await using var context = await ContextAsync();
        var dialogs = context.Render<MudDialogProvider>();
        await ExpenseAsync(context, 100m, _anaId, 100m);
        await ExpenseAsync(context, 50m, _anaId, 100m, new DateOnly(2026, 9, 5));
        var entries = context.Services.GetRequiredService<IEntryCatalog>();
        var rows = await entries.GetAsync();
        var result = await context.Services.GetRequiredService<IReimbursementReceipts>().ReceiveAsync(new(new DateOnly(2026, 10, 15), _accountId, rows.Select(x => new ReceiveReimbursementItem(x.ExternalId, new Money(Math.Abs(x.Amount.Amount)), new Money(Math.Abs(x.Amount.Amount)))).ToList()));
        Assert.True(result.Succeeded, result.Error);
        var page = context.Render<Reimbursements>();
        page.WaitForAssertion(() => Assert.Single(Rows(page)));

        _ = page.Find("[data-undo-receipt]").ClickAsync(new MouseEventArgs());
        dialogs.WaitForAssertion(() => Assert.Equal(2, dialogs.FindAll("[aria-label='Itens do recebimento'] li").Count));
        Assert.Contains("Conta Exemplo", dialogs.Find(".confirm-message").TextContent, StringComparison.Ordinal);
        Assert.Contains("R$ 150,00", dialogs.Find(".confirm-message").TextContent, StringComparison.Ordinal);

        await dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll(".confirm-message")));
        Assert.Single((await entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);

        _ = page.Find("[data-undo-receipt]").ClickAsync(new MouseEventArgs());
        dialogs.WaitForAssertion(() => Assert.NotEmpty(dialogs.FindAll("[data-confirm-ok]")));
        await dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Contains("Pendente", page.Find("[aria-label=Reembolsos]").TextContent, StringComparison.Ordinal));
        Assert.Empty((await entries.QueryAsync(new(Kind: CategoryKind.Income))).Entries);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }
}
