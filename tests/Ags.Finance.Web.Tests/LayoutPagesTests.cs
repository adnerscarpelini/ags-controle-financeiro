using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ags.Finance.Web.Tests;

/// <summary>Renderiza as páginas pelo servidor e confere o layout, a navegação e o uso offline.</summary>
public sealed partial class LayoutPagesTests : IDisposable
{
    private readonly AppFactory _factory = new();

    public static TheoryData<string, string> Sections => new()
    {
        { "lancamentos", "Lançamentos" },
        { "contas", "Contas" },
        { "faturas", "Faturas" },
        { "anexos", "Anexos" },
        { "reembolsos", "Reembolsos" },
        { "dashboard", "Dashboard" },
        { "planejamento", "Planejamento mensal" },
        { "relatorios/visao-19-meses", "Relatórios: visão de 19 meses" },
        { "cadastros/categorias", "Categorias e subcategorias" },
        { "configuracoes", "Configurações" },
    };

    [Theory]
    [MemberData(nameof(Sections))]
    public async Task SectionPageShowsItsTitleAndHighlightsItsMenuItem(string route, string title)
    {
        var html = await GetHtmlAsync($"/{route}");

        Assert.Matches($"<h1[^>]*>{title}</h1>", html);
        Assert.Contains($"<title>{title} · AGS Finanças</title>", html, StringComparison.Ordinal);

        var activeLinks = NavLinkRegex().Matches(html)
            .Where(match => match.Value.Contains("active", StringComparison.Ordinal))
            .Select(match => match.Value)
            .ToList();
        var active = Assert.Single(activeLinks);
        Assert.Contains($"href=\"{route}\"", active, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MenuListsEverySection()
    {
        var html = await GetHtmlAsync("/dashboard");

        foreach (var route in Sections.Select(row => (string)row[0]))
        {
            Assert.Matches($"<a[^>]*href=\"{route}\"[^>]*>", html);
        }
    }

    [Fact]
    public async Task TheOldMasterDataRouteRedirectsToCategories()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var response = await browser.GetAsync("/cadastros");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/cadastros/categorias", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/faturas", "/compromissos")]
    [InlineData("/faturas", "/cashback")]
    public async Task ScreensWithoutAMenuItemAreLinkedFromARelatedScreen(string page, string target)
    {
        var html = await GetHtmlAsync(page);

        Assert.Contains($"href=\"{target}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOldTransfersRouteRedirectsToAccounts()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var response = await browser.GetAsync("/transferencias");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/contas", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePendingRouteOpensTransactionsWithThePendingView()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var response = await browser.GetAsync("/pendentes");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/lancamentos?situacao=pendentes", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RootRedirectsToTheDashboard()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var response = await browser.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/dashboard", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PageDeclaresTheLogoAsFaviconAndLoadsOnlyLocalResources()
    {
        var html = await GetHtmlAsync("/dashboard");

        Assert.Contains("rel=\"icon\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"img/favicon.png\"", html, StringComparison.Ordinal);
        Assert.DoesNotMatch(ExternalReferenceRegex(), html);
    }

    [Fact]
    public async Task TheLibraryLoadsInTheApplicationButNotOnTheLoginPage()
    {
        var html = await GetHtmlAsync("/dashboard");
        Assert.Contains("_content/MudBlazor/MudBlazor.min.css", html, StringComparison.Ordinal);
        Assert.Contains("_content/MudBlazor/MudBlazor.min.js", html, StringComparison.Ordinal);

        using var client = _factory.CreateClient();
        var login = await client.GetStringAsync(new Uri("/login", UriKind.Relative));
        Assert.DoesNotContain("MudBlazor", login, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/css/tokens.css")]
    [InlineData("/css/app.css")]
    [InlineData("/fonts/inter-latin-variable.woff2")]
    [InlineData("/img/favicon.png")]
    [InlineData("/_content/MudBlazor/MudBlazor.min.css")]
    [InlineData("/_content/MudBlazor/MudBlazor.min.js")]
    [InlineData("/js/viewport.js")]
    public async Task StaticAssetsAreServedByTheApplication(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/css/tokens.css")]
    [InlineData("/css/app.css")]
    [InlineData("/_content/MudBlazor/MudBlazor.min.css")]
    public async Task StylesheetsDoNotReferenceExternalResources(string path)
    {
        using var client = _factory.CreateClient();

        var css = await client.GetStringAsync(new Uri(path, UriKind.Relative));
        css = LicenseCommentRegex().Replace(css, string.Empty);

        Assert.DoesNotContain("http://", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [GeneratedRegex(@"<a\b[^>]*\bmud-nav-link\b[^>]*>")]
    private static partial Regex NavLinkRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex LicenseCommentRegex();

    [GeneratedRegex(@"(?:src|href)\s*=\s*""(?:https?:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalReferenceRegex();

    private async Task<string> GetHtmlAsync(string path)
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();
        return await browser.GetHtmlAsync(path);
    }
}
