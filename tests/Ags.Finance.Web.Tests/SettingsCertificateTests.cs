using System.Net;
using System.Text.RegularExpressions;
using Ags.Finance.Web.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed partial class SettingsCertificateTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task SettingsShowsTheFingerprintAndTrustInstructionsForEveryPlatform()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();
        var html = await browser.GetHtmlAsync("/configuracoes");
        var expected = _factory.Services.GetRequiredService<ServerCertificate>().Info.Fingerprint;

        var shown = FingerprintRegex().Match(html);

        Assert.True(shown.Success);
        Assert.Equal(expected, shown.Groups[1].Value.Trim());
        foreach (var platform in new[] { "Windows", "Linux", "Android", "iPhone" })
        {
            Assert.Contains($"<summary>{platform}</summary>", html, StringComparison.Ordinal);
        }
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [GeneratedRegex("data-testid=\"fingerprint\"[^>]*>([^<]+)<")]
    private static partial Regex FingerprintRegex();
}
