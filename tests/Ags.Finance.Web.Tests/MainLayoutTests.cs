using System.Text.RegularExpressions;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Web.Components.Layout;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Ags.Finance.Web.Tests;

/// <summary>No celular o menu é uma gaveta: abre pelo botão e fecha ao navegar ou tocar na película.</summary>
public sealed class MainLayoutTests : IAsyncDisposable
{
    private readonly BunitContext _context = new BunitContext().UseAppUi().UseDefaultPreferences();

    [Fact]
    public void DrawerStartsClosedOnThePhone()
    {
        var layout = RenderLayout(isMobile: true);

        Assert.Contains("mud-drawer--closed", layout.Find(".sidebar").ClassList);
        Assert.Equal("false", MenuButton(layout).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void MenuButtonOpensTheDrawer()
    {
        var layout = RenderLayout(isMobile: true);

        MenuButton(layout).Click();

        Assert.Contains("mud-drawer--open", layout.Find(".sidebar").ClassList);
        Assert.Equal("true", MenuButton(layout).GetAttribute("aria-expanded"));
    }

    [Fact]
    public void MenuButtonClosesAnOpenDrawer()
    {
        var layout = RenderLayout(isMobile: true);
        MenuButton(layout).Click();

        MenuButton(layout).Click();

        Assert.Contains("mud-drawer--closed", layout.Find(".sidebar").ClassList);
    }

    [Fact]
    public void TappingTheScrimClosesTheDrawer()
    {
        var layout = RenderLayout(isMobile: true);
        MenuButton(layout).Click();

        layout.Find(".mud-overlay-drawer, .mud-drawer-overlay, .mud-overlay").Click();

        layout.WaitForAssertion(() => Assert.Contains("mud-drawer--closed", layout.Find(".sidebar").ClassList));
    }

    [Fact]
    public void ChoosingAMenuItemClosesTheDrawer()
    {
        var layout = RenderLayout(isMobile: true);
        MenuButton(layout).Click();

        layout.Find("a[href='faturas']").Click();

        Assert.Contains("mud-drawer--closed", layout.Find(".sidebar").ClassList);
    }

    [Fact]
    public void TheMenuStaysOpenOnTheDesktop()
    {
        var layout = RenderLayout(isMobile: false);

        Assert.Contains("mud-drawer--open", layout.Find(".sidebar").ClassList);
    }

    [Fact]
    public void MenuHasASignOutButtonThatPostsToTheSignOutEndpoint()
    {
        var layout = RenderLayout();

        var form = layout.Find("form.nav-footer");

        Assert.Equal("post", form.GetAttribute("method"));
        Assert.Equal("/account/sign-out", form.GetAttribute("action"));
        Assert.Equal("Sair", form.QuerySelector("button[type=submit]")?.TextContent.Trim());
    }

    [Fact]
    public void MenuHasTheSectionsInOrder()
    {
        var layout = RenderLayout();

        var labels = layout.FindAll(".nav-link span").Select(span => span.TextContent).ToList();

        Assert.Equal(
            ["Lançamentos", "Pendentes", "Transferências e saldos", "Faturas", "Anexos", "Cashback", "Reembolsos", "Dashboard", "Planejamento", "Relatórios", "Cadastros", "Configurações"],
            labels);
    }

    [Fact]
    public void TheLayoutProvidesTheLibraryProvidersSoDialogsAndTooltipsWork()
    {
        var layout = RenderLayout();

        Assert.Single(layout.FindComponents<MudThemeProvider>());
        Assert.Single(layout.FindComponents<MudPopoverProvider>());
        Assert.Single(layout.FindComponents<MudDialogProvider>());
        Assert.Single(layout.FindComponents<MudSnackbarProvider>());
    }

    [Theory]
    [InlineData(ThemePreference.Dark, true)]
    [InlineData(ThemePreference.Light, false)]
    public void SavingTheThemeInSettingsUpdatesTheLibraryWithoutReloading(ThemePreference theme, bool isDark)
    {
        var layout = RenderLayout();

        layout.InvokeAsync(() => _context.Services.GetRequiredService<ThemeState>().Apply(theme));

        layout.WaitForAssertion(() => Assert.Equal(isDark, ShowsDarkPalette(layout)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheAutomaticThemeFollowsTheSystem(bool systemIsDark)
    {
        var layout = RenderLayout();
        var provider = layout.FindComponent<MudThemeProvider>();

        layout.InvokeAsync(() => _context.Services.GetRequiredService<ThemeState>().Apply(ThemePreference.Auto));
        layout.InvokeAsync(() => provider.Instance.SystemDarkModeChangedAsync(systemIsDark));

        layout.WaitForAssertion(() => Assert.Equal(systemIsDark, ShowsDarkPalette(layout)));
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();

    private static bool ShowsDarkPalette(IRenderedComponent<MainLayout> layout)
    {
        var surface = Regex.Match(layout.Markup, @"--mud-palette-surface:\s*(?<color>[^;]+);");
        Assert.True(surface.Success, layout.Markup[..Math.Min(400, layout.Markup.Length)]);
        return surface.Groups["color"].Value.StartsWith("#1a1a1a", StringComparison.OrdinalIgnoreCase) || surface.Groups["color"].Value.Contains("26,26,26", StringComparison.Ordinal);
    }

    private static AngleSharp.Dom.IElement MenuButton(IRenderedComponent<MainLayout> layout) => layout.Find("button[aria-controls='sidebar']");

    private IRenderedComponent<MainLayout> RenderLayout(bool isMobile = false)
    {
        _context.JSInterop.Setup<bool>("agsViewport.watch", _ => true).SetResult(isMobile);
        return _context.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));
    }
}
