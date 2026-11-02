using System.Net;
using System.Text.RegularExpressions;

namespace Ags.Finance.Web.Tests;

/// <summary>Um navegador mínimo para os testes: cookies, formulários com token antifalsificação e redirecionamentos manuais.</summary>
internal sealed partial class Browser : IDisposable
{
    private readonly HttpClient _client;

    public Browser(HttpClient client)
    {
        _client = client;
    }

    public Task<HttpResponseMessage> GetAsync(string path)
    {
        return _client.GetAsync(new Uri(path, UriKind.Relative));
    }

    public async Task<string> GetHtmlAsync(string path)
    {
        using var response = await GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Envia um formulário com o token antifalsificação lido da página <paramref name="tokenPage"/>.</summary>
    public async Task<HttpResponseMessage> PostFormAsync(string path, string tokenPage, params (string Name, string Value)[] fields)
    {
        var token = await ReadTokenAsync(tokenPage);
        return await PostAsync(path, [.. fields, ("__RequestVerificationToken", token)]);
    }

    public Task<HttpResponseMessage> PostAsync(string path, params (string Name, string Value)[] fields)
    {
        var content = new FormUrlEncodedContent(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));
        return _client.PostAsync(new Uri(path, UriKind.Relative), content);
    }

    public Task<HttpResponseMessage> SignInAsync(string userName, string password, bool rememberMe = false, string? returnUrl = null)
    {
        List<(string, string)> fields = [("UserName", userName), ("Password", password)];
        if (rememberMe)
        {
            fields.Add(("RememberMe", "true"));
        }

        if (returnUrl is not null)
        {
            fields.Add(("ReturnUrl", returnUrl));
        }

        return PostFormAsync("/account/sign-in", "/login", [.. fields]);
    }

    public Task<HttpResponseMessage> SignOutAsync()
    {
        return PostFormAsync("/account/sign-out", "/configuracoes");
    }

    public Task<HttpResponseMessage> ChangePasswordAsync(string current, string next, string confirmation)
    {
        return PostFormAsync(
            "/account/password",
            "/configuracoes",
            ("CurrentPassword", current),
            ("NewPassword", next),
            ("ConfirmPassword", confirmation));
    }

    /// <summary>O destino do redirecionamento como caminho e query, venha absoluto (desafio do cookie) ou relativo.</summary>
    public static string Location(HttpResponseMessage response)
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task<string> ReadTokenAsync(string page)
    {
        var html = await GetHtmlAsync(page);
        var match = TokenRegex().Match(html);
        Assert.True(match.Success, $"A página {page} não tem token antifalsificação.");
        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenRegex();
}
