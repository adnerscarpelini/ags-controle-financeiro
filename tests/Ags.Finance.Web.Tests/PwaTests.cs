using System.Buffers.Binary;
using System.Net;
using System.Text.Json;

namespace Ags.Finance.Web.Tests;

public sealed class PwaTests
{
    private static async Task<(int Width, int Height)> PngSizeAsync(Browser browser, string path)
    {
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes.Take(4));
        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20)));
    }

    private static async Task<string> Text(Browser browser, string path)
    {
        using var response = await browser.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task ManifestIsPublicDeclaresTheDarkThemeAndValidIcons()
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        using var response = await anonymous.GetAsync("/manifest.webmanifest");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = manifest.RootElement;
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("/", root.GetProperty("start_url").GetString());
        Assert.Equal("pt-BR", root.GetProperty("lang").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("name").GetString()));
        var tokens = await Text(anonymous, "/css/tokens.css");
        // O manifesto usa a paleta escura (o lado escuro de light-dark(claro, escuro)).
        Assert.Matches($@"--color-background: light-dark\(#[0-9A-Fa-f]{{6}}, {System.Text.RegularExpressions.Regex.Escape(root.GetProperty("background_color").GetString()!)}\)", tokens);
        Assert.Matches($@"--color-accent: light-dark\(#[0-9A-Fa-f]{{6}}, {System.Text.RegularExpressions.Regex.Escape(root.GetProperty("theme_color").GetString()!)}\)", tokens);
        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.Contains(icons, x => x.GetProperty("purpose").GetString() == "any" && x.GetProperty("sizes").GetString() == "192x192");
        Assert.Contains(icons, x => x.GetProperty("purpose").GetString() == "any" && x.GetProperty("sizes").GetString() == "512x512");
        Assert.Contains(icons, x => x.GetProperty("purpose").GetString() == "maskable");
        foreach (var icon in icons)
        {
            var sizes = icon.GetProperty("sizes").GetString()!.Split('x');
            Assert.Equal((int.Parse(sizes[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(sizes[1], System.Globalization.CultureInfo.InvariantCulture)), await PngSizeAsync(anonymous, icon.GetProperty("src").GetString()!));
        }
        Assert.Equal((180, 180), await PngSizeAsync(anonymous, "/img/apple-touch-icon.png"));
    }

    [Fact]
    public async Task ServiceWorkerOnlyHandlesNavigationsAndNeverCachesData()
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        using var response = await anonymous.GetAsync("/sw.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        var script = await response.Content.ReadAsStringAsync();
        Assert.Contains("addEventListener('fetch'", script, StringComparison.Ordinal);
        Assert.Contains("mode !== 'navigate'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cache.put", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/api", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfflinePageSaysTheServerWasNotFoundAndItsAssetsAreServed()
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        var page = await anonymous.GetHtmlAsync("/offline.html");
        Assert.Contains("Não encontrei o servidor", page, StringComparison.Ordinal);
        Assert.Contains("Tentar novamente", page, StringComparison.Ordinal);
        foreach (var asset in new[] { "/css/tokens.css", "/img/icon-192.png", "/js/pwa.js" })
        {
            using var response = await anonymous.GetAsync(asset);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task EveryPageHeadLinksTheManifestAndRegistersTheServiceWorker()
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        var login = await anonymous.GetHtmlAsync("/login");
        Assert.Contains("rel=\"manifest\" href=\"manifest.webmanifest\"", login, StringComparison.Ordinal);
        Assert.Contains("rel=\"apple-touch-icon\"", login, StringComparison.Ordinal);
        Assert.Contains("name=\"apple-mobile-web-app-capable\"", login, StringComparison.Ordinal);
        Assert.Contains("js/pwa.js", login, StringComparison.Ordinal);
        Assert.Contains("viewport-fit=cover", login, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplicationPagesStillRequireLoginWhilePwaFilesStayPublic()
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        using var denied = await anonymous.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        foreach (var path in new[] { "/manifest.webmanifest", "/sw.js", "/offline.html" })
        {
            using var response = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task StylesheetKeepsTouchTargetsAndReadableFormControlsForMobile()
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        var css = await Text(anonymous, "/css/app.css");
        Assert.Contains("--touch-target", css, StringComparison.Ordinal);
        Assert.Contains("{ min-height: var(--touch-target); font-size: 16px; }", css, StringComparison.Ordinal);
        Assert.Contains("font: inherit", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767.98px)", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(auto-fit, minmax(min(100%, 220px), 1fr))", css, StringComparison.Ordinal);
    }
}
