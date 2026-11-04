using Ags.Finance.Web.Components;
using Ags.Finance.Web.Components.Common;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>HelpTip, EmptyState e ConfirmDialog: a base que as telas novas reaproveitam (#95).</summary>
public sealed class CommonComponentsTests : IAsyncDisposable
{
    private readonly BunitContext _context = new BunitContext().UseAppUi();

    public ValueTask DisposeAsync() => _context.DisposeAsync();

    [Fact]
    public void StatCardShowsTheTotalWithoutADecorativeIcon()
    {
        var card = _context.Render<StatCard>(parameters => parameters.Add(x => x.Label, "Receitas").Add(x => x.Value, "R$ 10,00").Add(x => x.ValueClass, ValueColor.Positive).Add(x => x.Hero, true));

        Assert.Equal("R$ 10,00", card.Find(".stat-value").TextContent);
        Assert.Contains(ValueColor.Positive, card.Find(".stat-value").ClassList);
        Assert.Contains("stat-hero", card.Find(".stat-card").ClassList);
        Assert.Empty(card.FindAll(".mud-icon-root"));
    }

    [Fact]
    public void HelpTipHasAKeyboardReachableButtonWithAnAccessibleName()
    {
        _context.Render<MudPopoverProvider>();

        var tip = _context.Render<HelpTip>(parameters => parameters.Add(x => x.Text, "Saldo atual somado às pendências do mês."));

        var button = tip.Find("button.help-tip-button");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("Ajuda: Saldo atual somado às pendências do mês.", button.GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData("keyboard")]
    [InlineData("touch")]
    [InlineData("mouse")]
    public void HelpTipOpensWithKeyboardTouchAndMouse(string input)
    {
        var popovers = _context.Render<MudPopoverProvider>();
        var tip = _context.Render<HelpTip>(parameters => parameters.Add(x => x.Text, "Texto de ajuda"));

        var button = tip.Find("button.help-tip-button");
        switch (input)
        {
            case "keyboard": button.FocusIn(); break;
            case "touch": button.PointerEnter(new PointerEventArgs { PointerType = "touch" }); button.Click(); break;
            default: button.PointerEnter(new PointerEventArgs { PointerType = "mouse" }); break;
        }

        popovers.WaitForAssertion(() => Assert.Contains("Texto de ajuda", popovers.Find(".mud-tooltip").TextContent, StringComparison.Ordinal));
        Assert.Contains("mud-popover-open", popovers.Find(".mud-tooltip").ClassList);
    }

    [Fact]
    public void ATouchDoesNotCloseTheHelpWhenTheFingerLeavesAndClosesWhenTouchingOutside()
    {
        var popovers = _context.Render<MudPopoverProvider>();
        var tip = _context.Render<HelpTip>(parameters => parameters.Add(x => x.Text, "Texto de ajuda"));
        var button = tip.Find("button.help-tip-button");

        button.Click();
        tip.Find("button.help-tip-button").PointerLeave(new PointerEventArgs { PointerType = "touch" });
        popovers.WaitForAssertion(() => Assert.Contains("mud-popover-open", popovers.Find(".mud-tooltip").ClassList));

        tip.Find("button.help-tip-button").FocusOut();
        popovers.WaitForAssertion(() => Assert.Empty(popovers.FindAll(".mud-tooltip.mud-popover-open")));
    }

    [Fact]
    public void EmptyStateShowsTheMessageAndRunsTheAction()
    {
        var clicked = false;
        var empty = _context.Render<EmptyState>(parameters => parameters
            .Add(x => x.Message, "Nenhum lançamento neste mês.")
            .Add(x => x.ActionText, "Novo lançamento")
            .Add(x => x.OnAction, EventCallback.Factory.Create(this, () => clicked = true)));

        Assert.Contains("Nenhum lançamento neste mês.", empty.Markup, StringComparison.Ordinal);
        empty.Find("button").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void EmptyStateWithoutActionHasNoButton()
    {
        var empty = _context.Render<EmptyState>(parameters => parameters.Add(x => x.Message, "Nada aqui."));

        Assert.Empty(empty.FindAll("button"));
    }

    [Theory]
    [InlineData("[data-confirm-ok]", true)]
    [InlineData("[data-confirm-cancel]", false)]
    public async Task ConfirmDialogOpensThroughTheDialogServiceAndReturnsTheChoice(string button, bool expected)
    {
        var provider = _context.Render<MudDialogProvider>();
        var dialogs = _context.Services.GetRequiredService<IDialogService>();

        var question = provider.InvokeAsync(() => ConfirmDialog.AskAsync(dialogs, "Excluir", "Esta ação não pode ser desfeita.", "Excluir", destructive: true));
        provider.WaitForAssertion(() => Assert.Contains("Esta ação não pode ser desfeita.", provider.Markup, StringComparison.Ordinal));
        provider.Find(button).Click();

        Assert.Equal(expected, await question.WaitAsync(TimeSpan.FromSeconds(10)));
    }
}
