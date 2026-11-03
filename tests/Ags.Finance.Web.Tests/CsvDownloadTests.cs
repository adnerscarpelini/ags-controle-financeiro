using System.Net;
using System.Text;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

public sealed class CsvDownloadTests
{
    [Theory]
    [InlineData("banks")]
    [InlineData("card-digits")]
    [InlineData("people")]
    public async Task EveryDownloadRequiresLoginAndServesTheOfficialCsv(string kind)
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        foreach (var suffix in new[] { string.Empty, "/modelo", "/exemplo" })
        {
            using var denied = await anonymous.GetAsync($"/csv/cadastros/{kind}{suffix}");
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        }
        using var browser = await factory.CreateSignedInBrowserAsync();
        foreach (var suffix in new[] { string.Empty, "/modelo", "/exemplo" })
        {
            using var response = await browser.GetAsync($"/csv/cadastros/{kind}{suffix}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            Assert.EndsWith(".csv", response.Content.Headers.ContentDisposition?.FileName ?? string.Empty, StringComparison.Ordinal);
            var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
            Assert.Contains("ags-finance-csv,", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExportReflectsTheRegistryAndUnknownKindIsNotFound()
    {
        using var factory = new AppFactory();
        using var browser = await factory.CreateSignedInBrowserAsync();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountCatalog>().CreateBankAsync("Banco Exemplo");
        }
        using var response = await browser.GetAsync("/csv/cadastros/banks");
        Assert.Contains("Banco Exemplo", Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()), StringComparison.Ordinal);
        using var missing = await browser.GetAsync("/csv/cadastros/xlsx");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("/csv/lancamentos", "transactions")]
    [InlineData("/csv/lancamentos/series", "recurring_series")]
    [InlineData("/csv/lancamentos/modelo", "transactions")]
    [InlineData("/csv/lancamentos/exemplo", "transactions")]
    [InlineData("/csv/lancamentos/series/modelo", "recurring_series")]
    [InlineData("/csv/lancamentos/series/exemplo", "recurring_series")]
    public async Task TransactionDownloadsRequireLoginAndServeTheOfficialLayout(string route, string type)
    {
        using var factory = new AppFactory();
        using var anonymous = factory.CreateBrowser();
        using var denied = await anonymous.GetAsync(route);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using var browser = await factory.CreateSignedInBrowserAsync();
        using var response = await browser.GetAsync(route + (route.EndsWith("series", StringComparison.Ordinal) || route == "/csv/lancamentos" ? "?month=2026-10&effective=true" : string.Empty));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"ags-finance-csv,{type},2", Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("month=2026-13")]
    [InlineData("from=01/10/2026")]
    [InlineData("account=nao-e-guid")]
    [InlineData("effective=talvez")]
    [InlineData("kind=Transfer2")]
    public async Task InvalidFilterValuesAreRejectedInsteadOfIgnored(string query)
    {
        using var factory = new AppFactory();
        using var browser = await factory.CreateSignedInBrowserAsync();
        using var response = await browser.GetAsync($"/csv/lancamentos?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
