using Ags.Finance.Modules.Reports.Contracts;
using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Dashboard;
using Bunit;
using Microsoft.AspNetCore.Components.Web;

namespace Ags.Finance.Web.Tests;

/// <summary>Os cards de pendências do Dashboard mostram 10 registros por vez, com navegação.</summary>
public sealed class PendingListTests
{
    private static List<PendingItem> Items(int count) => Enumerable.Range(1, count)
        .Select(number => new PendingItem(Guid.CreateVersion7(), new DateOnly(2026, 11, 1).AddDays(number), $"Conta {number}", new Money(number), false, Guid.CreateVersion7(), "Corrente Exemplo"))
        .ToList();

    private static IRenderedComponent<PendingList> Render(BunitContext context, IReadOnlyList<PendingItem> items) =>
        context.Render<PendingList>(parameters => parameters.Add(x => x.Items, items).Add(x => x.Empty, "Nada").Add(x => x.Label, "Pendências"));

    private static bool IsDisabled(IRenderedComponent<PendingList> list, string selector) => list.Find(selector).HasAttribute("disabled");

    [Fact]
    public async Task UpToTenItemsDoNotShowTheNavigation()
    {
        await using var context = new BunitContext().UseAppUi();

        var list = Render(context, Items(10));

        Assert.Equal(10, list.FindAll("li").Count);
        Assert.Empty(list.FindAll("[data-pending-next]"));
    }

    [Fact]
    public async Task TheNavigationMovesForwardAndBackAndStopsAtTheEnds()
    {
        await using var context = new BunitContext().UseAppUi();
        var list = Render(context, Items(23));

        Assert.Equal(10, list.FindAll("li").Count);
        Assert.Equal("1–10 de 23", list.Find("[data-pending-position]").TextContent);
        Assert.True(IsDisabled(list, "[data-pending-previous]"));

        await list.Find("[data-pending-next]").ClickAsync(new MouseEventArgs());
        Assert.Equal("11–20 de 23", list.Find("[data-pending-position]").TextContent);
        Assert.Contains("Conta 11", list.FindAll("li")[0].TextContent, StringComparison.Ordinal);

        await list.Find("[data-pending-next]").ClickAsync(new MouseEventArgs());
        Assert.Equal("21–23 de 23", list.Find("[data-pending-position]").TextContent);
        Assert.Equal(3, list.FindAll("li").Count);
        Assert.True(IsDisabled(list, "[data-pending-next]"));

        await list.Find("[data-pending-previous]").ClickAsync(new MouseEventArgs());
        Assert.Equal("11–20 de 23", list.Find("[data-pending-position]").TextContent);
    }

    [Fact]
    public async Task WhenTheListShrinksThePageComesBackToTheLastOne()
    {
        await using var context = new BunitContext().UseAppUi();
        var list = Render(context, Items(23));
        await list.Find("[data-pending-next]").ClickAsync(new MouseEventArgs());
        await list.Find("[data-pending-next]").ClickAsync(new MouseEventArgs());

        list.Render(parameters => parameters.Add(x => x.Items, Items(12)));

        Assert.Equal("11–12 de 12", list.Find("[data-pending-position]").TextContent);
    }
}
