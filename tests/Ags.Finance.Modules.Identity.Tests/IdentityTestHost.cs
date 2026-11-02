using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

/// <summary>O módulo Identity sobre um SQLite real em arquivo temporário, com relógio controlado pelo teste.</summary>
internal sealed class IdentityTestHost : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ags-finance-tests-").FullName;
    private readonly ServiceProvider _provider;
    private bool _migrated;

    public IdentityTestHost()
    {
        ConnectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(_directory, "finance.db") }.ToString();
        Clock = new ManualTimeProvider { Now = new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero) };

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddIdentityModule(ConnectionString);
        _provider = services.BuildServiceProvider();
    }

    public string ConnectionString { get; }

    public ManualTimeProvider Clock { get; }

    /// <summary>Executa o código em um escopo novo, como uma requisição ou um comando.</summary>
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await EnsureMigratedAsync();
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<List<string>> QueryAsync(string sql)
    {
        await EnsureMigratedAsync();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetValue(0).ToString() ?? string.Empty);
        }

        return values;
    }

    private async Task EnsureMigratedAsync()
    {
        if (_migrated)
        {
            return;
        }

        await _provider.MigrateIdentityModuleAsync();
        _migrated = true;
    }

    public void Dispose()
    {
        _provider.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}

internal sealed class ManualTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; }

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}
