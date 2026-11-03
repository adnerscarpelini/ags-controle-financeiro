using System.Net;
using Ags.Finance.Web.Components.Layout;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Ags.Finance.Web.Tests;

/// <summary>
/// Ocultar valores (#71). O mascaramento em si é JavaScript no navegador e foi verificado com um harness no Chrome;
/// aqui ficam o botão no layout (computador e celular) e a entrega dos scripts.
/// </summary>
public sealed class PrivacyToggleTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly AppFactory _factory = new();

    public void Dispose()
    {
        _context.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public void TheLayoutHasTheToggleInTheTopbarForPhonesAndInTheSidebarForTheDesktop()
    {
        var layout = _context.Render<MainLayout>(parameters => parameters.Add(x => x.Body, (RenderFragment)(builder => builder.AddContent(0, "conteúdo"))));

        var topbar = layout.Find(".topbar button[data-privacy-toggle]");
        var sidebar = layout.Find(".sidebar button[data-privacy-toggle]");

        foreach (var button in new[] { topbar, sidebar })
        {
            Assert.Equal("button", button.GetAttribute("type"));
            Assert.Equal("false", button.GetAttribute("aria-pressed"));
            Assert.Equal("Ocultar valores", button.GetAttribute("aria-label"));
        }

        Assert.Contains("icon-button", topbar.ClassList);
        Assert.Equal("Ocultar valores", sidebar.QuerySelector("[data-privacy-label]")!.TextContent);
    }

    [Fact]
    public async Task EveryAuthenticatedPageHasTheToggleAndLoadsThePrivacyScriptInTheHead()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        foreach (var page in new[] { "/dashboard", "/lancamentos", "/faturas", "/configuracoes" })
        {
            var html = await browser.GetHtmlAsync(page);

            Assert.Equal(2, html.Split("data-privacy-toggle").Length - 1);
            Assert.Contains("aria-pressed=\"false\"", html, StringComparison.Ordinal);
            var privacy = html.IndexOf("js/privacy.js", StringComparison.Ordinal);
            Assert.True(privacy > 0 && privacy < html.IndexOf("</head>", StringComparison.Ordinal), $"{page}: privacy.js deve estar no <head>.");
        }
    }

    [Theory]
    [InlineData("/js/privacy.js", "agsPrivacy", "ags.hide-values")]
    [InlineData("/js/theme.js", "theme-color", "agsTheme")]
    public async Task TheScriptsAreServedWithoutLogin(string path, string first, string second)
    {
        using var anonymous = _factory.CreateBrowser();

        using var response = await anonymous.GetAsync(path);
        var script = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(first, script, StringComparison.Ordinal);
        Assert.Contains(second, script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLoginPageDoesNotShowTheToggleButStillLoadsTheScripts()
    {
        using var anonymous = _factory.CreateBrowser();

        var html = await anonymous.GetHtmlAsync("/login");

        Assert.DoesNotContain("data-privacy-toggle", html, StringComparison.Ordinal);
        Assert.Contains("js/privacy.js", html, StringComparison.Ordinal);
        Assert.Contains("js/theme.js", html, StringComparison.Ordinal);
    }
}
