using Ags.Finance.Modules.Transactions.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ags.Finance.Modules.Transactions;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionsModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContext<TransactionsDbContext>((provider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(TransactionsDbContext.MigrationsHistoryTable, TransactionsDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        return services;
    }

    public static async Task MigrateTransactionsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
