using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components.Common;
using Ags.Finance.Web.Components.Entries;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>Cenário de tela de lançamentos com dados fictícios, banco real e os provedores de janela e tooltip.</summary>
internal sealed class EntryUi : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database;

    private readonly string _attachments;

    private EntryUi(PostgresTestDatabase database, BunitContext context, string attachments)
    {
        _database = database;
        _attachments = attachments;
        Context = context;
    }

    public BunitContext Context { get; }

    public IRenderedComponent<MudDialogProvider> Dialogs { get; private set; } = default!;

    public IRenderedComponent<MudPopoverProvider> Popovers { get; private set; } = default!;

    public Guid Account { get; private set; }

    public Guid Card { get; private set; }

    public Guid ExpenseSubcategory { get; private set; }

    public Guid IncomeSubcategory { get; private set; }

    public Guid Merchant { get; private set; }

    public Guid IncomeMerchant { get; private set; }

    public IEntryCatalog Entries => Context.Services.GetRequiredService<IEntryCatalog>();

    public string AttachmentsDirectory => _attachments;

    /// <summary>Trava as tabelas do schema até o descarte, parando no meio do carregamento a tela que as lê.</summary>
    public async Task<IAsyncDisposable> LockSchemaAsync(string schema)
    {
        var tables = await _database.QueryAsync($"SELECT quote_ident(schemaname) || '.' || quote_ident(tablename) FROM pg_tables WHERE schemaname = '{schema}' AND tablename <> '__ef_migrations'");
        var connection = new Npgsql.NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new Npgsql.NpgsqlCommand($"LOCK TABLE {string.Join(", ", tables)} IN ACCESS EXCLUSIVE MODE", connection, transaction);
        await command.ExecuteNonQueryAsync();
        return new SchemaLock(connection, transaction);
    }

    /// <summary>Espera até alguma consulta estar parada na trava de <see cref="LockSchemaAsync"/>.</summary>
    public async Task WaitForBlockedQueryAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (await _database.ScalarAsync("SELECT COUNT(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'") == "0")
        {
            Assert.True(DateTime.UtcNow < deadline, "Nenhuma consulta parou na trava.");
            await Task.Delay(20);
        }
    }

    public static async Task<EntryUi> CreateAsync(DateTimeOffset? now = null)
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var context = new BunitContext().UseAppUi();
        var attachments = Path.Combine(Path.GetTempPath(), "ags-entry-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(attachments);
        context.Services.AddSingleton<TimeProvider>(new FixedClock(now ?? new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)))
            .AddAccountsModule(database.ConnectionString)
            .AddTransactionsModule(database.ConnectionString)
            .AddReimbursementsModule(database.ConnectionString)
            .AddInvoicesModule(database.ConnectionString)
            .AddPreferencesModule(database.ConnectionString)
            .AddAttachmentsModule(database.ConnectionString, new AttachmentStorageOptions(attachments));
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateAttachmentsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateReimbursementsModuleAsync();
        await context.Services.MigrateInvoicesModuleAsync();
        await context.Services.MigratePreferencesModuleAsync();

        var ui = new EntryUi(database, context, attachments);
        await ui.SeedAsync();
        ui.Popovers = context.Render<MudPopoverProvider>();
        ui.Dialogs = context.Render<MudDialogProvider>();
        return ui;
    }

    public Task OpenNewAsync(EntryDialogType type, Guid? account = null) =>
        OpenAsync(windows => windows.ShowNewAsync(type, account));

    public Task OpenEditAsync(EntryDetails entry) =>
        OpenAsync(windows => windows.ShowEditAsync(entry));

    public async Task OpenTransferAsync(Guid? source = null)
    {
        await Dialogs.InvokeAsync(() => Context.Services.GetRequiredService<EntryDialogs>().ShowTransferAsync(source));
        Dialogs.WaitForAssertion(() => Assert.Contains("Corrente Exemplo", Dialogs.Find("#transfer-source").TextContent, StringComparison.Ordinal));
    }

    public void SetAmount(string value, string field = "#entry-amount")
    {
        Dialogs.Find(field).Input(value);
        Dialogs.WaitForAssertion(() => Assert.Equal(decimal.Parse(value, System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), Dialogs.FindComponent<MoneyField>().Instance.Value));
    }

    public async Task SetMerchantAsync(string name = "Loja Exemplo") => await Dialogs.Find("#entry-merchant").InputAsync(name);

    public async Task FillExpenseAsync(string amount = "150")
    {
        SetAmount(amount);
        await SetMerchantAsync();
        await Dialogs.Find("#entry-account").ChangeAsync(Account.ToString());
    }

    public Task SubmitAsync() => Dialogs.Find("#entry-form").SubmitAsync(EventArgs.Empty);

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_attachments)) { Directory.Delete(_attachments, true); }
    }

    private sealed class SchemaLock(Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private async Task OpenAsync(Func<EntryDialogs, Task<IDialogReference>> open)
    {
        await Dialogs.InvokeAsync(() => open(Context.Services.GetRequiredService<EntryDialogs>()));
        Dialogs.WaitForAssertion(() => Assert.Equal("true", Dialogs.Find("#entry-form").GetAttribute("data-ready")));
    }

    private async Task SeedAsync()
    {
        var accounts = Context.Services.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        Account = (await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Corrente Exemplo", AccountKind.Checking, "green"))).ExternalId!.Value;
        Card = (await accounts.SaveCreditCardAsync(new(null, Account, "Cartão Exemplo", CardBrand.Visa, new Money(10000), 3, 10, true, "green"))).ExternalId!.Value;
        var classifications = Context.Services.GetRequiredService<IClassificationCatalog>();
        var expense = await classifications.SaveCategoryAsync(null, "Mercado", CategoryKind.Expense);
        ExpenseSubcategory = (await classifications.SaveSubcategoryAsync(null, expense.ExternalId!.Value, "Feira")).ExternalId!.Value;
        var income = await classifications.SaveCategoryAsync(null, "Renda", CategoryKind.Income);
        IncomeSubcategory = (await classifications.SaveSubcategoryAsync(null, income.ExternalId!.Value, "Salário")).ExternalId!.Value;
        var merchants = Context.Services.GetRequiredService<IMerchantCatalog>();
        Merchant = (await merchants.SaveAsync(null, "Loja Exemplo", ExpenseSubcategory)).ExternalId!.Value;
        IncomeMerchant = (await merchants.SaveAsync(null, "Empresa Exemplo", IncomeSubcategory)).ExternalId!.Value;
    }

    public async Task<Guid> AddPersonAsync(decimal percent = 50m) =>
        (await Context.Services.GetRequiredService<IPersonCatalog>().SaveAsync(null, "Pessoa Exemplo", percent)).ExternalId!.Value;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
