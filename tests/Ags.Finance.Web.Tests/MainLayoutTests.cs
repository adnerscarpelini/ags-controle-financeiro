using System.Text.RegularExpressions;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Web.Components.Layout;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;

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

        layout.Find(".mud-overlay").Click();

        layout.WaitForAssertion(() => Assert.Contains("mud-drawer--closed", layout.Find(".sidebar").ClassList));
    }

    [Fact]
    public void ChoosingAMenuItemClosesTheDrawer()
    {
        var layout = RenderLayout(isMobile: true);
        MenuButton(layout).Click();

        layout.Find("a[href='cartoes']").Click();

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
    public void MenuHasTheSectionsInOrderWithTheGroupsInATree()
    {
        var layout = RenderLayout();

        var labels = layout.FindAll(".nav-tree .mud-nav-link-text, .nav-tree .mud-nav-link-text span").Select(x => x.TextContent.Trim()).Where(x => x.Length > 0).Distinct().ToList();

        Assert.Equal(
            ["Dashboard", "Lançamentos", "Lançamentos fixos", "Contas", "Cartões", "Planejamento", "Relatórios", "Reembolsos", "Cadastros", "Bancos", "Categorias", "Descrições", "Pessoas", "Mais opções", "Anexos", "Arquivos CSV", "Backups", "Configurações"],
            labels);
    }

    [Fact]
    public void ClickingAGroupOpensItsTreeAndClickingAgainClosesIt()
    {
        var layout = RenderLayout();
        Assert.Empty(layout.FindAll(".nav-group .mud-collapse-entered"));

        GroupButton(layout, "Cadastros").Click();
        layout.WaitForAssertion(() => Assert.Equal("true", GroupButton(layout, "Cadastros").GetAttribute("aria-expanded")));

        GroupButton(layout, "Cadastros").Click();
        layout.WaitForAssertion(() => Assert.Equal("false", GroupButton(layout, "Cadastros").GetAttribute("aria-expanded")));
    }

    [Fact]
    public void TheCurrentScreenOpensItsGroupAndIsHighlighted()
    {
        _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/cadastros/pessoas");

        var layout = RenderLayout();

        Assert.Equal("true", GroupButton(layout, "Cadastros").GetAttribute("aria-expanded"));
        Assert.Equal("false", GroupButton(layout, "Mais opções").GetAttribute("aria-expanded"));
        var active = Assert.Single(layout.FindAll("a.mud-nav-link.active"));
        Assert.Equal("cadastros/pessoas", active.GetAttribute("href"));
    }

    [Fact]
    public void NavigatingToAScreenOfAGroupOpensThatGroup()
    {
        var layout = RenderLayout();
        Assert.Equal("false", GroupButton(layout, "Mais opções").GetAttribute("aria-expanded"));

        layout.InvokeAsync(() => _context.Services.GetRequiredService<NavigationManager>().NavigateTo("/backups"));

        layout.WaitForAssertion(() => Assert.Equal("true", GroupButton(layout, "Mais opções").GetAttribute("aria-expanded")));
    }

    [Fact]
    public void NewShowsTheFourOptionsWithIconAndText()
    {
        var layout = RenderLayout();

        layout.Find("[data-new-entry-menu] button").Click();

        layout.WaitForAssertion(() =>
        {
            var options = layout.FindAll(".mud-popover-open .mud-menu-item").ToList();
            Assert.Equal(["Despesa", "Receita", "Despesa cartão", "Transferência"], options.Select(x => x.TextContent.Trim()));
            Assert.All(options, option => Assert.NotNull(option.QuerySelector("svg")));
        });
    }

    [Fact]
    public void CollapsingTheMenuShowsOnlyIconsAndRemembersTheChoiceInThisBrowser()
    {
        var store = _context.JSInterop.SetupVoid("agsMenu.setCollapsed", true);
        var layout = RenderLayout();

        layout.Find("[data-toggle-menu]").Click();

        layout.WaitForAssertion(() => Assert.Contains("sidebar-collapsed", layout.Find(".sidebar").ClassList));
        Assert.Contains("mud-drawer--closed", layout.Find(".sidebar").ClassList);
        store.VerifyInvoke("agsMenu.setCollapsed");
        Assert.Equal("Expandir menu", layout.Find("[data-toggle-menu]").GetAttribute("aria-label"));
        Assert.Empty(layout.FindAll(".nav-tree .mud-nav-link span:not(.visually-hidden)"));
    }

    [Fact]
    public void TheMenuStartsCollapsedWhenThatWasTheChoiceInThisBrowser()
    {
        _context.JSInterop.Setup<bool>("agsMenu.isCollapsed").SetResult(true);

        var layout = RenderLayout();

        layout.WaitForAssertion(() => Assert.Contains("sidebar-collapsed", layout.Find(".sidebar").ClassList));
    }

    [Fact]
    public void WithTheMenuCollapsedEveryIconHasAnAccessibleNameAndAGroupOpensAFloatingMenu()
    {
        _context.JSInterop.Setup<bool>("agsMenu.isCollapsed").SetResult(true);
        var layout = RenderLayout();
        layout.WaitForAssertion(() => Assert.Contains("sidebar-collapsed", layout.Find(".sidebar").ClassList));

        var names = layout.FindAll(".nav-tree .visually-hidden").Select(x => x.TextContent).ToList();
        Assert.Contains("Dashboard", names);
        Assert.Contains("Configurações", names);

        layout.Find(".nav-group-collapsed button[aria-label='Cadastros']").Click();

        layout.WaitForAssertion(() =>
            Assert.Equal(["Bancos", "Categorias", "Descrições", "Pessoas"], layout.FindAll(".mud-popover-open .mud-menu-item").Select(x => x.TextContent.Trim())));
    }

    [Fact]
    public void WithTheMenuCollapsedHoveringAnIconShowsItsName()
    {
        _context.JSInterop.Setup<bool>("agsMenu.isCollapsed").SetResult(true);
        var layout = RenderLayout();
        layout.WaitForAssertion(() => Assert.Contains("sidebar-collapsed", layout.Find(".sidebar").ClassList));

        layout.FindAll(".nav-tooltip").First(x => x.TextContent.Contains("Dashboard", StringComparison.Ordinal)).PointerEnter();

        layout.WaitForAssertion(() => Assert.Contains("Dashboard", layout.Find(".mud-tooltip.mud-popover-open").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void ThePhoneDrawerDoesNotOfferCollapsing()
    {
        var layout = RenderLayout(isMobile: true);

        Assert.Empty(layout.FindAll("[data-toggle-menu]"));
    }

    [Fact]
    public void TheFooterHasExplicitHideValuesAndSignOut()
    {
        var layout = RenderLayout();

        var footer = layout.Find("form.nav-footer");

        Assert.Contains("Ocultar valores", footer.TextContent, StringComparison.Ordinal);
        Assert.Contains("Sair", footer.TextContent, StringComparison.Ordinal);
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

        layout.InvokeAsync(() => _context.Services.GetRequiredService<ThemeState>().Apply(ThemePreference.Auto));
        layout.Instance.SystemThemeChanged(systemIsDark);

        layout.WaitForAssertion(() => Assert.Equal(systemIsDark, ShowsDarkPalette(layout)));
    }

    [Fact]
    public void TheSavedDarkThemeIgnoresALightSystem()
    {
        var layout = RenderLayout();

        // A biblioteca, observando o sistema, trocaria a paleta sozinha e as janelas abririam brancas no tema escuro.
        Assert.False(layout.FindComponent<MudThemeProvider>().Instance.GetState(provider => provider.ObserveSystemDarkModeChange));

        layout.Instance.SystemThemeChanged(false);

        layout.WaitForAssertion(() => Assert.True(ShowsDarkPalette(layout)));
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();

    private static bool ShowsDarkPalette(IRenderedComponent<MainLayout> layout)
    {
        var surface = Regex.Match(layout.Markup, @"--mud-palette-surface:\s*(?<color>[^;]+);");
        Assert.True(surface.Success, layout.Markup[..Math.Min(400, layout.Markup.Length)]);
        return surface.Groups["color"].Value.StartsWith("#222224", StringComparison.OrdinalIgnoreCase) || surface.Groups["color"].Value.Contains("34,34,36", StringComparison.Ordinal);
    }

    private static AngleSharp.Dom.IElement GroupButton(IRenderedComponent<MainLayout> layout, string label) =>
        layout.FindAll(".nav-group > button, .nav-group button.mud-nav-link").First(x => x.TextContent.Contains(label, StringComparison.Ordinal));

    private static AngleSharp.Dom.IElement MenuButton(IRenderedComponent<MainLayout> layout) => layout.Find("button[aria-controls='sidebar']");

    private IRenderedComponent<MainLayout> RenderLayout(bool isMobile = false)
    {
        _context.JSInterop.Setup<bool>("agsViewport.watch", _ => true).SetResult(isMobile);
        return _context.Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));
    }
}
