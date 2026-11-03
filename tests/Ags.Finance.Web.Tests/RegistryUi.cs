using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Tests.Support;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>Cenário comum das telas de cadastro: banco real, janelas e menus.</summary>
internal sealed class RegistryUi : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database;

    private RegistryUi(PostgresTestDatabase database, BunitContext context)
    {
        _database = database;
        Context = context;
        Popovers = context.Render<MudPopoverProvider>();
        Dialogs = context.Render<MudDialogProvider>();
    }

    public BunitContext Context { get; }

    public IRenderedComponent<MudDialogProvider> Dialogs { get; }

    public IRenderedComponent<MudPopoverProvider> Popovers { get; }

    public static async Task<RegistryUi> CreateAsync()
    {
        var database = await PostgresTestDatabase.CreateAsync();
        var context = new BunitContext().UseAppUi();
        context.Services.AddAccountsModule(database.ConnectionString).AddTransactionsModule(database.ConnectionString).AddReimbursementsModule(database.ConnectionString);
        await context.Services.MigrateAccountsModuleAsync();
        await context.Services.MigrateTransactionsModuleAsync();
        await context.Services.MigrateReimbursementsModuleAsync();
        return new RegistryUi(database, context);
    }

    public static List<IElement> Rows<TPage>(IRenderedComponent<TPage> page)
        where TPage : IComponent => page.FindAll(".data-table tbody tr").Where(x => x.QuerySelector("[data-action]") is not null).ToList();

    public static IElement Row<TPage>(IRenderedComponent<TPage> page, string text)
        where TPage : IComponent => Rows(page).Single(x => x.TextContent.Contains(text, StringComparison.Ordinal));

    public static void Click(IElement button) => _ = button.ClickAsync(new MouseEventArgs());

    public void WaitForConfirmation() => Dialogs.WaitForAssertion(() => Assert.NotEmpty(Dialogs.FindAll("[data-confirm-ok]")));

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _database.DisposeAsync();
    }
}
