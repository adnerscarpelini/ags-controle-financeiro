using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Tela geral de lançamentos fixos (#125). O relógio do cenário está em 01/09/2026.</summary>
public sealed class RecurringEntriesPageTests
{
    private static async Task SeedAsync(EntryUi ui)
    {
        Assert.True((await ui.Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 9, 10), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50), IsPaid: false)))).Succeeded);
        Assert.True((await ui.Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 9, 20), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(30), CardExternalId: ui.Card)))).Succeeded);
        Assert.True((await ui.Entries.CreateRecurringAsync(new(new(new DateOnly(2026, 9, 5), ui.IncomeMerchant, ui.IncomeSubcategory, ui.Account, new Money(1000), IsPaid: false)))).Succeeded);
    }

    [Fact]
    public async Task TheScreenListsEverythingAndFiltersByType()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);
        var page = ui.Context.Render<RecurringEntries>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("[data-fixed-amount]").Count));
        Assert.Equal(["R$ 1.000,00", "-R$ 50,00", "-R$ 30,00"], page.FindAll("[data-fixed-amount]").Select(x => x.TextContent));
        Assert.Equal("-R$ 80,00", page.Find("[data-stat=fixed-expenses] .stat-value").TextContent);
        Assert.Equal("R$ 1.000,00", page.Find("[data-stat=fixed-income] .stat-value").TextContent);

        await page.Find("[data-filter=card]").ClickAsync(new MouseEventArgs());
        Assert.Equal("-R$ 30,00", Assert.Single(page.FindAll("[data-fixed-amount]")).TextContent);
        await page.Find("[data-filter=account]").ClickAsync(new MouseEventArgs());
        Assert.Equal("-R$ 50,00", Assert.Single(page.FindAll("[data-fixed-amount]")).TextContent);
        await page.Find("[data-filter=income]").ClickAsync(new MouseEventArgs());
        Assert.Equal("R$ 1.000,00", Assert.Single(page.FindAll("[data-fixed-amount]")).TextContent);
    }

    [Fact]
    public async Task EditingChangesTheValueDayAndAccountFieldOnlyOutsideTheCard()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);
        var page = ui.Context.Render<RecurringEntries>();
        await page.InvokeAsync(() => { });
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("[data-fixed-amount]").Count));

        await page.Find("[data-filter=card]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-action=edit]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#recurring-series-form");
        Assert.Empty(ui.Dialogs.FindAll("#recurring-series-account"));
        await ui.Dialogs.Find("[data-confirm-cancel]").ClickAsync(new MouseEventArgs());

        await page.Find("[data-filter=account]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-action=edit]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#recurring-series-account");
        ui.Dialogs.Find("#recurring-series-amount").Input("75,50");
        await ui.Dialogs.Find("#recurring-series-day").ChangeAsync("15");
        await ui.Dialogs.Find("#recurring-series-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Equal("-R$ 75,50", Assert.Single(page.FindAll("[data-fixed-amount]")).TextContent));
        var september = (await ui.Entries.QueryAsync(new(Month: new ReferenceMonth(2026, 9), Kind: CategoryKind.Expense, AccountExternalId: ui.Account))).Entries.Single(x => x.CardExternalId is null);
        Assert.Equal((new Money(-75.50m), new DateOnly(2026, 9, 15)), (september.Amount, september.Date));
    }

    [Fact]
    public async Task TransfersAppearInThePanelOutOfTheTotalsAndCanBeEditedAndDeleted()
    {
        await using var ui = await EntryUi.CreateAsync();
        var accounts = ui.Context.Services.GetRequiredService<IAccountCatalog>();
        var destination = await accounts.SaveAccountAsync(new(null, (await accounts.GetBanksAsync())[0].ExternalId, "Segunda Conta", AccountKind.Checking, "green"));
        var transfers = ui.Context.Services.GetRequiredService<IRecurringTransferCatalog>();
        Assert.True((await transfers.CreateAsync(new(ui.Account, destination.ExternalId!.Value, new DateOnly(2026, 9, 3), new Money(100), null, false))).Succeeded);
        var page = ui.Context.Render<RecurringEntries>();
        page.WaitForAssertion(() => Assert.Contains("Transferência para Segunda Conta", page.Markup, StringComparison.Ordinal));
        Assert.Equal("R$ 0,00", page.Find("[data-stat=fixed-expenses] .stat-value").TextContent);
        await page.Find("[data-filter=transfer]").ClickAsync(new MouseEventArgs());
        Assert.Equal("R$ 100,00", Assert.Single(page.FindAll("[data-fixed-amount]")).TextContent);

        await page.Find("[data-action=edit]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("#recurring-transfer-series-form");
        ui.Dialogs.Find("#recurring-transfer-series-amount").Input("150,00");
        await ui.Dialogs.Find("#recurring-transfer-series-form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Equal("R$ 150,00", Assert.Single(page.FindAll("[data-fixed-amount]")).TextContent));
        Assert.Equal(new Money(150), Assert.Single(await transfers.GetMonthAsync(new ReferenceMonth(2026, 9))).Amount);

        _ = page.Find("[data-action=delete]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("[data-confirm-ok]");
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-fixed-amount]")));
        Assert.Empty(await transfers.GetSeriesAsync());
    }

    [Fact]
    public async Task SelectedSeriesAreDeletedTogetherAfterConfirming()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);
        var page = ui.Context.Render<RecurringEntries>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("[data-fixed-amount]").Count));

        var boxes = page.FindAll("td input.row-select");
        await boxes[1].ChangeAsync(true);
        await page.FindAll("td input.row-select")[2].ChangeAsync(true);
        _ = page.Find("[data-delete-selected]").ClickAsync(new MouseEventArgs());
        ui.Dialogs.WaitForElement("[data-confirm-ok]");
        await ui.Dialogs.Find("[data-confirm-ok]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal("R$ 1.000,00", Assert.Single(page.FindAll("[data-fixed-amount]")).TextContent));
        Assert.Empty(page.FindAll("[data-delete-selected]"));
    }
}
