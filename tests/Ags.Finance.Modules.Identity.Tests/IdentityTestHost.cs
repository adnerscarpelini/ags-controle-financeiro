using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Identity.Tests;

/// <summary>O módulo Identity sobre um PostgreSQL real e vazio, com relógio controlado pelo teste.</summary>
internal sealed class IdentityTestHost : IDisposable
{
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;

    public IdentityTestHost()
    {
        Clock = new ManualTimeProvider { Now = new DateTimeOffset(2027, 1, 15, 10, 0, 0, TimeSpan.Zero) };
    }

    public ManualTimeProvider Clock { get; }

    /// <summary>Executa o código em um escopo novo, como uma requisição ou um comando.</summary>
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        var provider = await EnsureMigratedAsync();
        await using var scope = provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<List<string>> QueryAsync(string sql)
    {
        await EnsureMigratedAsync();
        return await _database!.QueryAsync(sql);
    }

    private async Task<ServiceProvider> EnsureMigratedAsync()
    {
        if (_provider is not null)
        {
            return _provider;
        }

        _database = await PostgresTestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddIdentityModule(_database.ConnectionString);
        var provider = services.BuildServiceProvider();
        await provider.MigrateIdentityModuleAsync();
        _provider = provider;
        return provider;
    }

    public void Dispose()
    {
        _provider?.Dispose();
        _database?.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
