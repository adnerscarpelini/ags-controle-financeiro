using Ags.Finance.Modules.Invoices.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ags.Finance.Modules.Invoices;

public static class DependencyInjection
{
    public static IServiceCollection AddInvoicesModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContext<InvoicesDbContext>((provider, options) => options
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable(InvoicesDbContext.MigrationsHistoryTable))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        return services;
    }

    public static async Task MigrateInvoicesModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InvoicesDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
