using System.Net;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ags.Finance.Web.Tests;

/// <summary>Como cada preferência (#69) aparece no HTML que o navegador recebe.</summary>
public sealed class PreferencesIntegrationTests : IDisposable
{
    private readonly AppFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task SaveAsync(Func<AppPreferences, AppPreferences> change)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var preferences = scope.ServiceProvider.GetRequiredService<IPreferences>();
        var result = await preferences.SaveAsync(change(await preferences.GetAsync()));
        Assert.True(result.Succeeded, result.Error);
    }

    [Theory]
    [InlineData(ThemePreference.Dark, "dark")]
    [InlineData(ThemePreference.Light, "light")]
    [InlineData(ThemePreference.Auto, "auto")]
    public async Task TheSavedThemeIsInTheInitialHtmlOfTheLoginAndOfAuthenticatedPages(ThemePreference theme, string expected)
    {
        await SaveAsync(x => x with { Theme = theme });
        using var anonymous = _factory.CreateBrowser();
        using var signedIn = await _factory.CreateSignedInBrowserAsync();

        var login = await anonymous.GetHtmlAsync("/login");
        var dashboard = await signedIn.GetHtmlAsync("/dashboard");

        Assert.StartsWith("<!DOCTYPE html>", login.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"<html lang=\"pt-BR\" data-theme=\"{expected}\">", login, StringComparison.Ordinal);
        Assert.Contains($"<html lang=\"pt-BR\" data-theme=\"{expected}\">", dashboard, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutSavedPreferencesTheDarkThemeAppliesAndTheThemeScriptIsServed()
    {
        using var anonymous = _factory.CreateBrowser();

        var login = await anonymous.GetHtmlAsync("/login");
        using var script = await anonymous.GetAsync("/js/theme.js");

        Assert.Contains("data-theme=\"dark\"", login, StringComparison.Ordinal);
        Assert.Contains("js/theme.js", login, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Contains("agsTheme", await script.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StartPage.Dashboard, "/dashboard")]
    [InlineData(StartPage.Transactions, "/lancamentos")]
    [InlineData(StartPage.Invoices, "/cartoes")]
    public async Task TheRootRedirectsToTheChosenStartPage(StartPage start, string expected)
    {
        await SaveAsync(x => x with { StartPage = start });
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var root = await browser.GetAsync("/");

        Assert.EndsWith(expected, Browser.Location(root), StringComparison.Ordinal);
        using var direct = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
    }

    [Fact]
    public async Task SettingsShowsEveryPreferenceWithItsDefaultAndKeepsTheOtherSections()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        var html = await browser.GetHtmlAsync("/configuracoes");

        Assert.Contains("id=\"preferences-form\"", html, StringComparison.Ordinal);
        Assert.Matches("name=\"pref-theme\" value=\"dark\"[^>]*checked", html);
        Assert.Matches("name=\"pref-recurring\" value=\"on-date\"[^>]*checked", html);
        foreach (var id in new[] { "pref-start-page", "pref-pending-window", "pref-top-categories", "pref-page-size", "pref-session-hours", "pref-section-categories" })
        {
            Assert.Contains($"id=\"{id}\"", html, StringComparison.Ordinal);
        }

        Assert.Contains("Trocar senha", html, StringComparison.Ordinal);
        Assert.Contains("Backup e restauração", html, StringComparison.Ordinal);
        Assert.Contains("Certificado HTTPS", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DashboardShowsOnlyTheEnabledSections()
    {
        await SaveAsync(x => x with { Sections = new(Summary: true, Pending: false, Categories: true, Invoices: false) });
        using var browser = await _factory.CreateSignedInBrowserAsync();

        var html = await browser.GetHtmlAsync("/dashboard");

        Assert.Contains("id=\"summary-title\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"expense-categories-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"pending-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"invoice-report-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"no-sections\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DashboardWithEverySectionOffExplainsAndLinksToTheSettings()
    {
        await SaveAsync(x => x with { Sections = new(false, false, false, false) });
        using var browser = await _factory.CreateSignedInBrowserAsync();

        var html = await browser.GetHtmlAsync("/dashboard");

        Assert.Contains("data-testid=\"no-sections\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"configuracoes\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"summary-title\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(30)]
    public async Task DashboardPendingSectionNamesThePreferredWindow(int days)
    {
        await SaveAsync(x => x with { PendingWindowDays = days });
        using var browser = await _factory.CreateSignedInBrowserAsync();

        var html = await browser.GetHtmlAsync("/dashboard");

        Assert.Contains($"Próximos {days} dias", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public async Task CategoryChartHasThePreferredCountButTheTableStaysComplete(int top)
    {
        await SaveAsync(x => x with { TopCategories = top });
        using var browser = await _factory.CreateSignedInBrowserAsync();
        await SeedAsync(entries: 12, distinctSubcategories: true);

        var html = await browser.GetHtmlAsync("/dashboard");

        Assert.Contains($"Despesas por categoria: {top} maiores", html, StringComparison.Ordinal);
        var chart = html[html.IndexOf("class=\"bar-chart\"", StringComparison.Ordinal)..];
        chart = chart[..chart.IndexOf("</ol>", StringComparison.Ordinal)];
        Assert.Equal(top, chart.Split("<li>").Length - 1);
        var table = html[html.IndexOf("id=\"expense-categories-title\"", StringComparison.Ordinal)..];
        table = table[..table.IndexOf("</tbody>", StringComparison.Ordinal)];
        Assert.Equal(12, table.Split("<th scope=\"row\">").Length - 1);
    }

    [Theory]
    [InlineData(25, 3)]
    [InlineData(100, 1)]
    public async Task TransactionListUsesThePreferredPageSizeWithoutChangingTheTotals(int pageSize, int pages)
    {
        await SaveAsync(x => x with { PageSize = pageSize });
        using var browser = await _factory.CreateSignedInBrowserAsync();
        await SeedAsync(entries: 60, distinctSubcategories: false);

        var html = await browser.GetHtmlAsync("/lancamentos");

        Assert.Contains($"60 lançamentos · Página 1 de {pages}", html, StringComparison.Ordinal);
        Assert.Contains("-R$ 60,00", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(12, false)]
    [InlineData(24, false)]
    [InlineData(168, false)]
    [InlineData(12, true)]
    [InlineData(168, true)]
    public async Task TheLoginTicketExpiresAfterTheChosenSessionLengthOrThirtyDaysWhenRemembered(int hours, bool remember)
    {
        await SaveAsync(x => x with { SessionHours = hours });
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();
        var before = DateTimeOffset.UtcNow;

        using var signIn = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password, rememberMe: remember);

        var expires = TicketExpiry(signIn);
        var expected = remember ? TimeSpan.FromDays(30) : TimeSpan.FromHours(hours);
        Assert.InRange(expires, before + expected - TimeSpan.FromMinutes(1), DateTimeOffset.UtcNow + expected + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ChangingTheSessionLengthDoesNotChangeAnAlreadyOpenSession()
    {
        await _factory.CreateUserAsync();
        using var first = _factory.CreateBrowser();
        using var firstSignIn = await first.SignInAsync(AppFactory.UserName, AppFactory.Password);
        var firstExpiry = TicketExpiry(firstSignIn);

        await SaveAsync(x => x with { SessionHours = 168 });
        using var again = await first.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.False(again.Headers.Contains("Set-Cookie"), "A sessão aberta não deve ser reemitida.");
        using var second = _factory.CreateBrowser();
        using var secondSignIn = await second.SignInAsync(AppFactory.UserName, AppFactory.Password);
        Assert.True(TicketExpiry(secondSignIn) - firstExpiry > TimeSpan.FromDays(6));
    }

    private DateTimeOffset TicketExpiry(HttpResponseMessage signIn)
    {
        var cookie = signIn.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("ags.session=", StringComparison.Ordinal));
        var value = Uri.UnescapeDataString(cookie["ags.session=".Length..cookie.IndexOf(';', StringComparison.Ordinal)]);
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var ticket = options.TicketDataFormat.Unprotect(value);
        Assert.NotNull(ticket);
        return ticket.Properties.ExpiresUtc ?? throw new InvalidOperationException("O ticket não tem expiração.");
    }

    /// <summary>Cria despesas avulsas de R$ 1,00 na conta, no mês corrente, para os testes de lista e gráfico.</summary>
    private async Task SeedAsync(int entries, bool distinctSubcategories)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var accounts = provider.GetRequiredService<IAccountCatalog>();
        var bank = await accounts.CreateBankAsync("Banco Exemplo");
        var account = await accounts.SaveAccountAsync(new(null, bank.ExternalId!.Value, "Conta Exemplo", AccountKind.Checking, "green"));
        var classifications = provider.GetRequiredService<IClassificationCatalog>();
        var merchants = provider.GetRequiredService<IMerchantCatalog>();
        var catalog = provider.GetRequiredService<IEntryCatalog>();
        var today = DateOnly.FromDateTime(provider.GetRequiredService<TimeProvider>().GetLocalNow().DateTime);
        var subcategories = new List<Guid>();
        for (var index = 0; index < (distinctSubcategories ? entries : 1); index++)
        {
            var category = await classifications.SaveCategoryAsync(null, $"Categoria {index:00}", CategoryKind.Expense);
            var subcategory = await classifications.SaveSubcategoryAsync(null, category.ExternalId!.Value, $"Sub {index:00}");
            subcategories.Add(subcategory.ExternalId!.Value);
        }

        var merchant = await merchants.SaveAsync(null, "Loja Exemplo", subcategories[0]);
        for (var index = 0; index < entries; index++)
        {
            var result = await catalog.CreateAsync(new(today, merchant.ExternalId!.Value, subcategories[distinctSubcategories ? index : 0], account.ExternalId!.Value, new Money(1)));
            Assert.True(result.Succeeded, result.Error);
        }
    }
}
