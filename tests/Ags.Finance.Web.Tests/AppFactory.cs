using Ags.Finance.Modules.Identity.Contracts;
using Ags.Finance.Tests.Support;
using Ags.Finance.Web.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ags.Finance.Web.Tests;

/// <summary>A aplicação inteira com um PostgreSQL vazio e uma pasta de dados temporária, ambos apagados no fim.</summary>
internal sealed class AppFactory : WebApplicationFactory<App>
{
    public const string UserName = "maria";
    public const string Password = "senha-segura-123";

    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "ags-finance-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ILoggerProvider? _loggerProvider;
    private readonly PostgresTestDatabase _database = PostgresTestDatabase.CreateAsync().GetAwaiter().GetResult();

    public AppFactory(ILoggerProvider? loggerProvider = null)
    {
        _loggerProvider = loggerProvider;
    }

    /// <summary>Cria o usuário único, como o comando <c>user create</c> faria no computador do serviço.</summary>
    public async Task CreateUserAsync(string userName = UserName, string password = Password)
    {
        await using var scope = Services.CreateAsyncScope();
        var status = await scope.ServiceProvider.GetRequiredService<ICreateUser>().ExecuteAsync(userName, password);
        Assert.Equal(CreateUserStatus.Created, status);
    }

    /// <summary>Um navegador novo: guarda cookies, fala HTTPS (o cookie de sessão é Secure) e não segue redirecionamentos.</summary>
    public Browser CreateBrowser()
    {
        return new Browser(CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        }));
    }

    /// <summary>Cria o usuário (se ainda não existe) e devolve um navegador já logado.</summary>
    public async Task<Browser> CreateSignedInBrowserAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            if (!await scope.ServiceProvider.GetRequiredService<IUserExists>().ExecuteAsync())
            {
                await CreateUserAsync();
            }
        }

        var browser = CreateBrowser();
        using var response = await browser.SignInAsync(UserName, Password);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return browser;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dataDirectory + "-attachments");
        builder.UseSetting("Attachments:Directory", _dataDirectory + "-attachments");
        builder.UseSetting("DataDirectory", _dataDirectory);
        builder.UseSetting("ConnectionStrings:Finance", _database.ConnectionString);

        if (_loggerProvider is not null)
        {
            builder.ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddProvider(_loggerProvider);
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (Directory.Exists(_dataDirectory + "-attachments")) { Directory.Delete(_dataDirectory + "-attachments", true); }
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
    }
}
