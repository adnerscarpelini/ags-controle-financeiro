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
        { "faturas", "Faturas" },
        { "cashback", "Cashback" },
        { "reembolsos", "Reembolsos" },
        { "dashboard", "Dashboard" },
        { "cadastros", "Cadastros" },
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

        foreach (var (route, title) in Sections.Select(row => ((string)row[0], (string)row[1])))
        {
            Assert.Matches($"<a[^>]*href=\"{route}\"[^>]*>", html);
            Assert.Contains(title, html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RootRedirectsToTheDashboard()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

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

    [Theory]
    [InlineData("/css/tokens.css")]
    [InlineData("/css/app.css")]
    [InlineData("/fonts/inter-latin-variable.woff2")]
    [InlineData("/img/favicon.png")]
    public async Task StaticAssetsAreServedByTheApplication(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/css/tokens.css")]
    [InlineData("/css/app.css")]
    public async Task StylesheetsDoNotReferenceExternalResources(string path)
    {
        using var client = _factory.CreateClient();

        var css = await client.GetStringAsync(new Uri(path, UriKind.Relative));

        Assert.DoesNotContain("http://", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", css, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [GeneratedRegex(@"<a\b[^>]*\bclass=""[^""]*nav-link[^""]*""[^>]*>|<a\b[^>]*\bhref=""[^""]*""[^>]*\bclass=""[^""]*nav-link[^""]*""[^>]*>")]
    private static partial Regex NavLinkRegex();

    [GeneratedRegex(@"(?:src|href)\s*=\s*""(?:https?:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalReferenceRegex();

    private async Task<string> GetHtmlAsync(string path)
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync(new Uri(path, UriKind.Relative));
        return WebUtility.HtmlDecode(html);
    }
}
