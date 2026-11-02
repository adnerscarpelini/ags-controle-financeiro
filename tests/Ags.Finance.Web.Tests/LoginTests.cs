using System.Net;
using System.Text.RegularExpressions;
using Ags.Finance.Modules.Identity.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Tests;

/// <summary>Os critérios de aceite do login (#36), pelo navegador simulado.</summary>
public sealed partial class LoginTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task CorrectCredentialsOpenTheDashboard()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var signIn = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password);

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Equal("/", signIn.Headers.Location?.OriginalString);
        using var root = await browser.GetAsync("/");
        Assert.EndsWith("/dashboard", root.Headers.Location?.OriginalString, StringComparison.Ordinal);
        var dashboard = await browser.GetHtmlAsync("/dashboard");
        Assert.Matches("<h1[^>]*>Dashboard</h1>", dashboard);
    }

    [Fact]
    public async Task AWrongPasswordShowsAGenericMessage()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var signIn = await browser.SignInAsync(AppFactory.UserName, "senha-errada-123");

        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        Assert.Equal("/login?error=invalid", signIn.Headers.Location?.OriginalString);
        var page = await browser.GetHtmlAsync(signIn.Headers.Location!.OriginalString);
        Assert.Contains("Usuário ou senha incorretos.", page, StringComparison.Ordinal);
        using var dashboard = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
    }

    [Fact]
    public async Task AnUnknownUserGetsTheSameAnswerAsAWrongPassword()
    {
        await _factory.CreateUserAsync();
        using var wrongPassword = _factory.CreateBrowser();
        using var unknownUser = _factory.CreateBrowser();

        using var first = await wrongPassword.SignInAsync(AppFactory.UserName, "senha-errada-123");
        using var second = await unknownUser.SignInAsync("joao", AppFactory.Password);

        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(first.Headers.Location, second.Headers.Location);
    }

    [Fact]
    public async Task FiveWrongAttemptsBlockEvenTheCorrectPassword()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var wrong = await browser.SignInAsync(AppFactory.UserName, "senha-errada-123");
            Assert.Equal("/login?error=invalid", wrong.Headers.Location?.OriginalString);
        }

        using var fifth = await browser.SignInAsync(AppFactory.UserName, "senha-errada-123");
        Assert.Equal("/login?error=locked", fifth.Headers.Location?.OriginalString);

        using var correct = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password);
        Assert.Equal("/login?error=locked", correct.Headers.Location?.OriginalString);
        Assert.DoesNotContain(correct.Headers, header => header.Key == "Set-Cookie" && header.Value.Any(value => value.StartsWith("ags.session=", StringComparison.Ordinal)));
        using var dashboard = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);

        var page = await browser.GetHtmlAsync("/login?error=locked");
        Assert.Contains("Muitas tentativas incorretas", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DirectAccessGoesToTheLoginAndComesBackToThePageAfterSigningIn()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var direct = await browser.GetAsync("/lancamentos");
        Assert.Equal(HttpStatusCode.Redirect, direct.StatusCode);
        var loginUrl = Browser.Location(direct);
        Assert.Equal("/login?ReturnUrl=%2Flancamentos", loginUrl);

        var page = await browser.GetHtmlAsync(loginUrl);
        Assert.Contains("name=\"ReturnUrl\" value=\"/lancamentos\"", page, StringComparison.Ordinal);

        using var signIn = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password, returnUrl: "/lancamentos");
        Assert.Equal("/lancamentos", signIn.Headers.Location?.OriginalString);
        var transactions = await browser.GetHtmlAsync("/lancamentos");
        Assert.Matches("<h1[^>]*>Lançamentos</h1>", transactions);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("lancamentos")]
    public async Task OnlyPathsOfTheApplicationAreAcceptedAsReturnUrl(string returnUrl)
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var signIn = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password, returnUrl: returnUrl);

        Assert.Equal("/", signIn.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task WithoutKeepConnectedTheCookieIsASessionCookie()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var signIn = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password);

        var cookie = SessionCookie(signIn);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KeepConnectedMakesTheCookiePersistentForThirtyDays()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();
        var before = DateTimeOffset.UtcNow;

        using var signIn = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password, rememberMe: true);

        var expires = CookieExpiry(SessionCookie(signIn));
        Assert.InRange(expires, before.AddDays(30).AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(30).AddMinutes(1));
    }

    [Fact]
    public async Task TheSessionCookieIsHttpOnlySecureAndSameSite()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var signIn = await browser.SignInAsync(AppFactory.UserName, AppFactory.Password);

        var cookie = SessionCookie(signIn).ToLowerInvariant();
        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SigningOutEndsTheSessionAndBackShowsNoData()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();
        using var before = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var signOut = await browser.SignOutAsync();

        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        Assert.Equal("/login", signOut.Headers.Location?.OriginalString);
        using var after = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("/login", Browser.Location(after), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PagesWithDataAreNeverStoredByTheBrowser()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var response = await browser.GetAsync("/dashboard");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLoginPageIsNotStoredByTheBrowserEither()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var response = await browser.GetAsync("/login");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangingThePasswordKeepsThisSessionAndEndsTheOthers()
    {
        await _factory.CreateUserAsync();
        using var thisBrowser = _factory.CreateBrowser();
        using var otherBrowser = _factory.CreateBrowser();
        await thisBrowser.SignInAsync(AppFactory.UserName, AppFactory.Password);
        await otherBrowser.SignInAsync(AppFactory.UserName, AppFactory.Password);

        using var change = await thisBrowser.ChangePasswordAsync(AppFactory.Password, "nova-senha-456", "nova-senha-456");

        Assert.Equal("/configuracoes?password=changed", change.Headers.Location?.OriginalString);
        using var thisSession = await thisBrowser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, thisSession.StatusCode);
        using var otherSession = await otherBrowser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, otherSession.StatusCode);
        Assert.StartsWith("/login", Browser.Location(otherSession), StringComparison.Ordinal);

        using var fresh = _factory.CreateBrowser();
        using var oldPassword = await fresh.SignInAsync(AppFactory.UserName, AppFactory.Password);
        Assert.StartsWith("/login?error=", oldPassword.Headers.Location?.OriginalString, StringComparison.Ordinal);
        using var newPassword = await fresh.SignInAsync(AppFactory.UserName, "nova-senha-456");
        Assert.Equal("/", newPassword.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("senha-errada-123", "nova-senha-456", "nova-senha-456", "wrong")]
    [InlineData(AppFactory.Password, "curta", "curta", "short")]
    [InlineData(AppFactory.Password, "nova-senha-456", "outra-senha-789", "mismatch")]
    public async Task ChangingThePasswordIsRefusedWithAClearOutcome(string current, string next, string confirmation, string outcome)
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var change = await browser.ChangePasswordAsync(current, next, confirmation);

        Assert.Equal($"/configuracoes?password={outcome}", change.Headers.Location?.OriginalString);
        var page = await browser.GetHtmlAsync(change.Headers.Location!.OriginalString);
        Assert.Contains("data-testid=\"password-message\"", page, StringComparison.Ordinal);
        using var fresh = _factory.CreateBrowser();
        using var signIn = await fresh.SignInAsync(AppFactory.UserName, AppFactory.Password);
        Assert.Equal("/", signIn.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ResettingThePasswordByTheCommandEndsOpenSessions()
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();
        using var open = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var status = await scope.ServiceProvider.GetRequiredService<IResetPassword>().ExecuteAsync("nova-senha-456");
            Assert.Equal(ResetPasswordStatus.Reset, status);
        }

        using var after = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
    }

    [Fact]
    public async Task TheLoginPageShowsTheFormWhenThereIsAUser()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        var page = await browser.GetHtmlAsync("/login");

        Assert.Contains("name=\"UserName\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"Password\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"RememberMe\"", page, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"current-password\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"no-user\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutAUserTheLoginPageExplainsTheCommandAndOffersNoForm()
    {
        using var browser = _factory.CreateBrowser();

        var page = await browser.GetHtmlAsync("/login");

        Assert.Contains("data-testid=\"no-user\"", page, StringComparison.Ordinal);
        Assert.Contains("user create", page, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Password\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static string SessionCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values));
        return Assert.Single(values, value => value.StartsWith("ags.session=", StringComparison.Ordinal));
    }

    private static DateTimeOffset CookieExpiry(string setCookie)
    {
        var match = ExpiresRegex().Match(setCookie);
        Assert.True(match.Success, $"O cookie não tem 'expires': {setCookie}");
        return DateTimeOffset.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("expires=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpiresRegex();
}
