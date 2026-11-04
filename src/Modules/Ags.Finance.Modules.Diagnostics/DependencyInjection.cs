using Ags.Finance.Modules.Diagnostics.Application.RecordError;
using Ags.Finance.Modules.Diagnostics.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ags.Finance.Modules.Diagnostics;

public static class DependencyInjection
{
    public static IServiceCollection AddDiagnosticsModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        // Sem log do EF Core aqui: com o banco fora do ar, cada gravação recusada viraria outro erro no console.
        services.AddDbContextFactory<DiagnosticsDbContext>((provider, options) => options
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MigrationsHistoryTable(DiagnosticsDbContext.MigrationsHistoryTable, DiagnosticsDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddSingleton<ErrorQueue>();
        services.AddSingleton<IErrorStore, ErrorStore>();
        services.AddSingleton<ILoggerProvider, DatabaseErrorLoggerProvider>();
        services.AddHostedService<ErrorRecorder>();
        return services;
    }

    public static async Task MigrateDiagnosticsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<DiagnosticsDbContext>>().CreateDbContextAsync(cancellationToken);
        await using (context)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
