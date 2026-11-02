using Ags.Finance.Modules.Reimbursements.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ags.Finance.Modules.Reimbursements;

public static class DependencyInjection
{
    public static IServiceCollection AddReimbursementsModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContext<ReimbursementsDbContext>((provider, options) => options
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable(ReimbursementsDbContext.MigrationsHistoryTable))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        return services;
    }

    public static async Task MigrateReimbursementsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ReimbursementsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
