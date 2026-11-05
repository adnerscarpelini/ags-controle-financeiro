using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>A opção de agrupar os lançamentos do cartão em Lançamentos. O relógio do cenário está em 01/09/2026.</summary>
public sealed class TransactionPageCardGroupingTests
{
    private static async Task SeedAsync(EntryUi ui)
    {
        for (var day = 1; day <= 3; day++)
        {
            Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, day), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(10), CardExternalId: ui.Card))).Succeeded);
        }

        Assert.True((await ui.Entries.CreateAsync(new(new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(50)))).Succeeded);
    }

    private static int RowCount(IRenderedComponent<Transactions> page) =>
        page.FindAll(".data-table tbody tr").Count(x => x.QuerySelector("[data-status]") is not null);

    [Fact]
    public async Task TheOptionConsolidatesTheCardAndIsKeptInThePreferences()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Equal(4, RowCount(page)));
        Assert.Equal("false", page.Find("[data-group-cards]").GetAttribute("aria-pressed"));
        var expenses = page.Find("[data-stat='expenses'] .stat-value").TextContent;

        await page.Find("[data-group-cards]").ClickAsync(new MouseEventArgs());

        page.WaitForAssertion(() => Assert.Equal(2, RowCount(page)));
        var group = page.FindAll(".data-table tbody tr").Single(x => x.QuerySelector("[data-status='card-group']") is not null);
        Assert.Contains("Cartão Exemplo", group.TextContent, StringComparison.Ordinal);
        Assert.Contains("Agrupado · 3 lançamentos", group.TextContent, StringComparison.Ordinal);
        Assert.Contains("-R$ 30,00", group.TextContent, StringComparison.Ordinal);
        Assert.NotNull(group.QuerySelector("[data-action='open-invoice']"));
        Assert.Null(group.QuerySelector("input.row-select"));
        Assert.Equal(expenses, page.Find("[data-stat='expenses'] .stat-value").TextContent);
        Assert.Contains("2 linhas", page.Find("[data-page-summary]").TextContent, StringComparison.Ordinal);
        Assert.True((await ui.Context.Services.GetRequiredService<IPreferences>().GetAsync()).GroupCardEntries);

        var reopened = ui.Context.Render<Transactions>();
        reopened.WaitForAssertion(() => Assert.Equal(2, RowCount(reopened)));
        Assert.Equal("true", reopened.Find("[data-group-cards]").GetAttribute("aria-pressed"));

        await reopened.Find("[data-group-cards]").ClickAsync(new MouseEventArgs());
        reopened.WaitForAssertion(() => Assert.Equal(4, RowCount(reopened)));
        Assert.False((await ui.Context.Services.GetRequiredService<IPreferences>().GetAsync()).GroupCardEntries);
    }

    [Fact]
    public async Task SeeingTheInvoiceOpensTheMonthOfTheGroup()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);
        var page = ui.Context.Render<Transactions>();
        page.WaitForAssertion(() => Assert.Equal(4, RowCount(page)));
        await page.Find("[data-group-cards]").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(2, RowCount(page)));

        await page.Find("[data-action='open-invoice']").ClickAsync(new MouseEventArgs());

        var address = ui.Context.Services.GetRequiredService<NavigationManager>().Uri;
        Assert.EndsWith($"/cartoes/{ui.Card}/faturas?mes=2026-09", address, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnThePhoneTheGroupIsACardWithTheSum()
    {
        await using var ui = await EntryUi.CreateAsync();
        await SeedAsync(ui);
        var preferences = ui.Context.Services.GetRequiredService<IPreferences>();
        Assert.True((await preferences.SaveAsync(await preferences.GetAsync() with { GroupCardEntries = true })).Succeeded);

        var page = ui.Context.Render<CascadingValue<bool>>(parameters => parameters
            .Add(x => x.Name, "IsMobile").Add(x => x.Value, true).AddChildContent<Transactions>());

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-entry-card]").Count));
        Assert.Equal("-R$ 30,00", page.Find("[data-card-group] [data-entry-card-amount]").TextContent);
    }
}
