using System.Net;
using Ags.Finance.Web.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ags.Finance.Web.Tests;

/// <summary>Segurança do login: nada de dados sem sessão, nenhuma rota que crie usuário e nenhuma senha em log.</summary>
public sealed class AccessControlTests : IDisposable
{
    private readonly AppFactory _factory = new();

    /// <summary>Cada página roteável do projeto, para uma página nova nascer coberta por estes testes.</summary>
    public static IEnumerable<string> Routes()
    {
        return typeof(App).Assembly.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: false).Cast<RouteAttribute>())
            .Select(route => route.Template)
            .Where(template => template != "/login")
            .Distinct()
            .Order();
    }

    public static TheoryData<string> DataPages()
    {
        var data = new TheoryData<string>();
        foreach (var route in Routes())
        {
            data.Add(route);
        }

        return data;
    }

    [Fact]
    public void EveryRoutablePageIsListed()
    {
        Assert.Contains("/dashboard", Routes());
        Assert.Contains("/lancamentos", Routes());
        Assert.Contains("/configuracoes", Routes());
    }

    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task WithoutSessionEveryPageRedirectsToTheLogin(string route)
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var response = await browser.GetAsync(route);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login?ReturnUrl=", Browser.Location(response), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(DataPages))]
    public async Task WithoutAnyUserRegisteredNoDataPageIsReachableEither(string route)
    {
        using var browser = _factory.CreateBrowser();

        using var response = await browser.GetAsync(route);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", Browser.Location(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCircuitEndpointRequiresLoginToo()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var response = await browser.PostAsync("/_blazor/negotiate?negotiateVersion=1");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EveryEndpointOfTheApplicationRequiresLoginExceptTheLoginPage()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();
        using var warmUp = await browser.GetAsync("/login");

        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
        var anonymous = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Order()
            .ToList();

        // Sem [AllowAnonymous] explícito vale a política de fallback (exigir usuário autenticado).
        var application = anonymous.Where(path => path is not null && !path.StartsWith("/_", StringComparison.Ordinal));
        Assert.Equal(["/account/sign-in", "/login"], application);
    }

    [Fact]
    public async Task TheAccountEndpointsThatChangeStateAreAnOpenListOfThree()
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();
        using var warmUp = await browser.GetAsync("/login");

        var writes = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
                && methods.HttpMethods.Any(method => method != HttpMethods.Get))
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(path => path is not null && !path.StartsWith("/_", StringComparison.Ordinal))
            .Where(path => path != "/login" && !Routes().Contains(path))
            .Order()
            .ToList();

        Assert.Equal(["/account/password", "/account/sign-in", "/account/sign-out"], writes);
    }

    [Theory]
    [InlineData("/register")]
    [InlineData("/signup")]
    [InlineData("/cadastro")]
    [InlineData("/account/register")]
    [InlineData("/account/create")]
    [InlineData("/account/user")]
    [InlineData("/user")]
    [InlineData("/user/create")]
    [InlineData("/users")]
    [InlineData("/api/users")]
    [InlineData("/Identity/Account/Register")]
    public async Task NoRouteCreatesAUserWhetherSignedInOrNot(string path)
    {
        await _factory.CreateUserAsync();
        using var anonymous = _factory.CreateBrowser();
        using var signedIn = await _factory.CreateSignedInBrowserAsync();

        foreach (var browser in new[] { anonymous, signedIn })
        {
            using var response = await browser.PostAsync(path, ("UserName", "intruso"), ("Password", "senha-do-intruso-1"));
            Assert.False(response.IsSuccessStatusCode, $"POST {path} respondeu {(int)response.StatusCode}");
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var status = await scope.ServiceProvider.GetRequiredService<Modules.Identity.Contracts.ICreateUser>()
            .ExecuteAsync("outro", "senha-segura-123");
        Assert.Equal(Modules.Identity.Contracts.CreateUserStatus.UserAlreadyExists, status);
    }

    [Fact]
    public async Task ChangingThePasswordWithoutASessionDoesNothing()
    {
        await _factory.CreateUserAsync();
        using var anonymous = _factory.CreateBrowser();
        using var login = await anonymous.GetAsync("/login");

        using var response = await anonymous.PostFormAsync(
            "/account/password",
            "/login",
            ("CurrentPassword", AppFactory.Password),
            ("NewPassword", "senha-do-intruso-1"),
            ("ConfirmPassword", "senha-do-intruso-1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", Browser.Location(response), StringComparison.Ordinal);
        using var owner = _factory.CreateBrowser();
        using var signIn = await owner.SignInAsync(AppFactory.UserName, AppFactory.Password);
        Assert.Equal("/", signIn.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/account/sign-in")]
    [InlineData("/account/sign-out")]
    [InlineData("/account/password")]
    public async Task FormsWithoutTheAntiforgeryTokenAreRefused(string path)
    {
        using var browser = await _factory.CreateSignedInBrowserAsync();

        using var response = await browser.PostAsync(
            path,
            ("UserName", AppFactory.UserName),
            ("Password", AppFactory.Password),
            ("CurrentPassword", AppFactory.Password),
            ("NewPassword", "nova-senha-456"),
            ("ConfirmPassword", "nova-senha-456"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var dashboard = await browser.GetAsync("/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
    }

    [Theory]
    [InlineData("/css/app.css")]
    [InlineData("/css/tokens.css")]
    [InlineData("/fonts/inter-latin-variable.woff2")]
    [InlineData("/img/favicon.png")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/login")]
    public async Task StaticFilesAndTheLoginPageNeedNoSession(string path)
    {
        await _factory.CreateUserAsync();
        using var browser = _factory.CreateBrowser();

        using var response = await browser.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NoPasswordIsEverWrittenToTheLog()
    {
        var logs = new CapturingLoggerProvider();
        using var appFactory = new AppFactory(logs);
        await appFactory.CreateUserAsync();
        using var browser = appFactory.CreateBrowser();

        await browser.SignInAsync(AppFactory.UserName, "senha-errada-123");
        await browser.SignInAsync(AppFactory.UserName, AppFactory.Password);
        await browser.ChangePasswordAsync(AppFactory.Password, "nova-senha-456", "nova-senha-456");
        await browser.ChangePasswordAsync("senha-errada-123", "outra-senha-789", "outra-senha-789");

        Assert.NotEmpty(logs.Messages);
        foreach (var secret in new[] { AppFactory.Password, "senha-errada-123", "nova-senha-456", "outra-senha-789" })
        {
            Assert.DoesNotContain(logs.Messages, message => message.Contains(secret, StringComparison.Ordinal));
        }
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return [.. _messages];
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(this);
        }

        public void Dispose()
        {
        }

        private void Add(string message)
        {
            lock (_messages)
            {
                _messages.Add(message);
            }
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly CapturingLoggerProvider _provider;

            public CapturingLogger(CapturingLoggerProvider provider)
            {
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _provider.Add(formatter(state, exception) + exception);
            }
        }
    }
}
