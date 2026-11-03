using Ags.Finance.SharedKernel;
using Ags.Finance.Web.Components.Common;
using Ags.Finance.Web.Components.Entries;
using Ags.Finance.Web.Components.Layout;
using Ags.Finance.Web.Components.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>Lógica da interface no celular (#103). O bUnit não mede layout: tamanhos e rolagem precisam de um navegador real.</summary>
public sealed class MobileLayoutTests
{
    private static async Task AddAsync(EntryUi ui, decimal amount, bool isPaid = true, DateOnly? date = null) =>
        Assert.True((await ui.Entries.CreateAsync(new(date ?? new DateOnly(2026, 9, 1), ui.Merchant, ui.ExpenseSubcategory, ui.Account, new Money(amount), IsPaid: isPaid))).Succeeded);

    private static IRenderedComponent<Transactions> RenderTransactions(EntryUi ui) =>
        ui.Context.Render<Transactions>(parameters => parameters.AddCascadingValue("IsMobile", true));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FormWindowsFillTheScreenOnlyOnThePhone(bool isMobile)
    {
        var viewport = new ViewportState();
        viewport.Set(isMobile);

        var options = AppDialogs.Options(viewport);

        Assert.Equal(isMobile, options.FullScreen);
        Assert.True(options.FullWidth);
        Assert.False(options.CloseOnEscapeKey);
        Assert.False(options.BackdropClick);
    }

    [Fact]
    public void TablesBecomeCardsOnlyOnThePhone()
    {
        Assert.Equal(Breakpoint.None, AppTables.Breakpoint(false));
        Assert.Equal(Breakpoint.Md, AppTables.Breakpoint(true));
    }

    [Fact]
    public async Task EachEntryAppearsInACardWithDescriptionDateSituationAndValue()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, 50, isPaid: false);

        var page = RenderTransactions(ui);

        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-entry-card]")));
        var card = page.Find("[data-entry-card]");
        Assert.Contains("Loja Exemplo", card.TextContent, StringComparison.Ordinal);
        Assert.Equal("01/09/2026 · Pendente", card.QuerySelector("[data-entry-card-meta]")!.TextContent);
        Assert.Equal("-R$ 50,00", card.QuerySelector("[data-entry-card-amount]")!.TextContent);
        Assert.Empty(page.FindAll(".data-table"));
    }

    [Fact]
    public async Task OnThePhoneTheActionsOfACardAreInAMenu()
    {
        await using var ui = await EntryUi.CreateAsync();
        ui.Context.Services.GetRequiredService<ViewportState>().Set(true);
        await AddAsync(ui, 50, isPaid: false);
        var page = RenderTransactions(ui);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-entry-card]")));
        Assert.Empty(page.FindAll("[data-entry-card] [data-action]"));

        await page.Find("[data-entry-card] .row-actions-menu button").ClickAsync(new MouseEventArgs());

        ui.Popovers.WaitForAssertion(() => Assert.Equal(["Pagar", "Editar", "Anexar", "Duplicar", "Excluir"], ui.Popovers.FindAll(".mud-popover-open .mud-menu-item").Select(x => x.TextContent.Trim())));
    }

    [Fact]
    public async Task TheSortIsASelectorOnThePhone()
    {
        await using var ui = await EntryUi.CreateAsync();
        await AddAsync(ui, 50);
        await AddAsync(ui, 300, date: new DateOnly(2026, 9, 2));
        var page = RenderTransactions(ui);
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("[data-entry-card]").Count));
        Assert.Contains("-R$ 300,00", page.FindAll("[data-entry-card]")[0].TextContent, StringComparison.Ordinal);

        await page.Find("#mobile-sort").ChangeAsync("Amount:desc");

        page.WaitForAssertion(() => Assert.Contains("-R$ 50,00", page.FindAll("[data-entry-card]")[0].TextContent, StringComparison.Ordinal));
        Assert.Equal("Amount:desc", page.Find("#mobile-sort").GetAttribute("value"));
    }

    [Fact]
    public async Task AnEmptyMonthStillGuidesOnThePhone()
    {
        await using var ui = await EntryUi.CreateAsync();

        var page = RenderTransactions(ui);

        page.WaitForAssertion(() => Assert.Contains("Nenhum lançamento", page.Find(".empty-state").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NewIsAlwaysReachableOnThePhoneAndTheDrawerClosesWhenChoosingAScreen()
    {
        await using var context = new BunitContext().UseAppUi().UseDefaultPreferences();
        context.JSInterop.Setup<bool>("agsViewport.watch", _ => true).SetResult(true);
        var layout = context.Render<MainLayout>(parameters => parameters.Add(x => x.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));

        layout.WaitForAssertion(() => Assert.NotNull(layout.Find("[data-new-entry-fab] button")));
        await layout.Find("[data-new-entry-fab] button").ClickAsync(new MouseEventArgs());
        layout.WaitForAssertion(() => Assert.Equal(["Despesa", "Receita", "Despesa cartão", "Transferência"], layout.FindAll(".mud-popover-open .mud-menu-item").Select(x => x.TextContent.Trim())));

        await layout.Find("button[aria-controls='sidebar']").ClickAsync(new MouseEventArgs());
        Assert.Contains("mud-drawer--open", layout.Find(".sidebar").ClassList);
        await layout.Find("a[href='cadastros/pessoas']").ClickAsync(new MouseEventArgs());
        Assert.Contains("mud-drawer--closed", layout.Find(".sidebar").ClassList);
    }

    [Fact]
    public async Task TheNewButtonIsNotDuplicatedOnTheDesktop()
    {
        await using var context = new BunitContext().UseAppUi().UseDefaultPreferences();

        var layout = context.Render<MainLayout>(parameters => parameters.Add(x => x.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));

        Assert.Empty(layout.FindAll("[data-new-entry-fab]"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ThePhoneFormOpensFullScreen()
    {
        await using var ui = await EntryUi.CreateAsync();
        ui.Context.Services.GetRequiredService<ViewportState>().Set(true);

        await ui.OpenNewAsync(EntryDialogType.Expense);

        Assert.NotEmpty(ui.Dialogs.FindAll(".mud-dialog-fullscreen"));
        Assert.Equal("decimal", ui.Dialogs.Find("#entry-amount").GetAttribute("inputmode"));
    }

    [Fact]
    public void TheStylesheetKeepsTouchTargetsFullScreenActionsAndNoPageHorizontalScroll()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ags.Finance.slnx"))) { directory = directory.Parent; }
        Assert.NotNull(directory);
        var css = File.ReadAllText(Path.Combine(directory.FullName, "src", "Ags.Finance.Web", "wwwroot", "css", "app.css"));
        var phone = css[css.LastIndexOf("@media (max-width: 767.98px)", StringComparison.Ordinal)..];

        Assert.Contains("overflow-x: clip", phone, StringComparison.Ordinal);
        Assert.Contains(".mud-icon-button { min-width: var(--touch-target); min-height: var(--touch-target); }", phone, StringComparison.Ordinal);
        Assert.Contains(".help-tip-button { width: var(--touch-target); height: var(--touch-target); }", phone, StringComparison.Ordinal);
        Assert.Contains(".mud-dialog-fullscreen .mud-dialog-actions { position: sticky; bottom: 0;", phone, StringComparison.Ordinal);
    }
}
