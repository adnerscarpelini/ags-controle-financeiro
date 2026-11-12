using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Transferências e ajustes de saldo em Lançamentos (#141). O relógio do cenário está em 01/09/2026.</summary>
public sealed class TransactionPageMovementTests
{
    private static async Task<Guid> SeedAsync(EntryUi ui)
    {
        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50)))).Succeeded);
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var bank = Assert.Single(await accounts.GetBanksAsync());
        var savings = (await accounts.SaveAccountAsync(new(null, bank.ExternalId, "Poupança Exemplo", AccountKind.Savings, "green"))).ExternalId!.Value;
        var transfer = await ui.Context.Services.GetRequiredService<IAccountMovementCatalog>()
            .TransferAsync(new(Guid.CreateVersion7(), savings, ui.Account, new DateOnly(2026, 9, 1), new Money(100)));
        Assert.True(transfer.Succeeded, transfer.Error);
        return savings;
    }

    private static List<AngleSharp.Dom.IElement> Rows(IRenderedComponent<Transactions> page) =>
        page.FindAll(".data-table tbody tr").Where(x => x.QuerySelector("[data-status]") is not null).ToList();

    private static List<AngleSharp.Dom.IElement> MovementRows(IRenderedComponent<Transactions> page) =>
        Rows(page).Where(x => x.QuerySelector("[data-movement]") is not null).ToList();

    [Fact]
    public async Task TheTransferShowsOneNeutralRowPerAccountWithoutChangingTheTotals()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);

        var page = ui.Context.Render<Transactions>();

        page.WaitForAssertion(() => Assert.Equal(3, Rows(page).Count));
        var legs = MovementRows(page);
        var outgoing = Assert.Single(legs, x => x.TextContent.Contains("Transferência para Corrente Exemplo", StringComparison.Ordinal));
        Assert.Contains("Poupança Exemplo", outgoing.TextContent, StringComparison.Ordinal);
        Assert.Contains("-R$ 100,00", outgoing.TextContent, StringComparison.Ordinal);
        var incoming = Assert.Single(legs, x => x.TextContent.Contains("Transferência de Poupança Exemplo", StringComparison.Ordinal));
        Assert.Contains("+R$ 100,00", incoming.TextContent, StringComparison.Ordinal);
        Assert.All(legs, x => Assert.NotNull(x.QuerySelector(".value-neutral")));
        Assert.All(legs, x => Assert.Null(x.QuerySelector("input.row-select")));
        Assert.All(legs, x => Assert.Null(x.QuerySelector("[data-action]")));
        Assert.Equal("-R$ 50,00", page.Find("[data-stat='expenses'] .stat-value").TextContent);
        Assert.Equal("R$ 0,00", page.Find("[data-stat='income'] .stat-value").TextContent);
    }

    [Fact]
    public async Task TheTransfersChipKeepsOnlyTheMovementsAndTheExpensesChipHidesThem()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Equal(3, Rows(page).Count));

        await page.Find("[data-type-filter='transfers']").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(2, Rows(page).Count));
        Assert.Equal(2, MovementRows(page).Count);
        Assert.Equal("true", page.Find("[data-type-filter='transfers']").GetAttribute("aria-pressed"));

        await page.Find("[data-type-filter='expense']").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Single(Rows(page)));
        Assert.Empty(MovementRows(page));
    }

    [Fact]
    public async Task APendingRecurringTransferCanBeConfirmedFromTheRow()
    {
        await using var ui = await EntryUi.CreateAsync();
        var savings = await SeedAsync(ui);
        var transfers = ui.Context.Services.GetRequiredService<IRecurringTransferCatalog>();
        Assert.True((await transfers.CreateAsync(new(ui.Account, savings, new DateOnly(2026, 9, 1), new Money(30), null, false))).Succeeded);
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Equal(4, MovementRows(page).Count));
        var pending = MovementRows(page).Where(x => x.QuerySelector("[data-status='pending']") is not null).ToList();
        Assert.Equal(2, pending.Count);
        Assert.All(pending, x => Assert.Contains("Recorrente", x.TextContent, StringComparison.Ordinal));

        await pending[0].QuerySelector("[data-action='confirm']")!.ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForAssertion(() => Assert.NotEmpty(ui.Dialogs.FindAll("#recurring-transfer-form")));
        await ui.Dialogs.Find("#recurring-transfer-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.DoesNotContain(MovementRows(page), x => x.QuerySelector("[data-status='pending']") is not null));
        var occurrence = Assert.Single(await transfers.GetMonthAsync(new ReferenceMonth(2026, 9)));
        Assert.True(occurrence.IsConfirmed);
    }

    [Fact]
    public async Task OnThePhoneTheMovementIsACardWithTheSignedAmount()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);

        var page = ui.Context.Render<CascadingValue<bool>>(parameters => parameters
            .Add(x => x.Name, "IsMobile").Add(x => x.Value, true).AddChildContent<Transactions>());

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-movement]").Count));
        Assert.Contains(page.FindAll("[data-movement] [data-entry-card-amount]"), x => x.TextContent == "-R$ 100,00");
    }
}
