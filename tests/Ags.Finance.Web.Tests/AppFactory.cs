using Ags.Finance.Web.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Ags.Finance.Web.Tests;

/// <summary>A aplicação inteira com uma pasta de dados temporária, apagada no fim.</summary>
internal sealed class AppFactory : WebApplicationFactory<App>
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "ags-finance-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DataDirectory", _dataDirectory);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
    }
}
