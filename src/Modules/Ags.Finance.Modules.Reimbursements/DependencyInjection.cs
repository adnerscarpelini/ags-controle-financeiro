using Ags.Finance.Modules.Reimbursements.Application;
using Ags.Finance.Modules.Reimbursements.Application.CalculateReimbursements;
using Ags.Finance.Modules.Reimbursements.Application.ManagePeople;
using Ags.Finance.Modules.Reimbursements.Application.ManageSharingRules;
using Ags.Finance.Modules.Reimbursements.Application.RegisterReceipt;
using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Infrastructure;
using Ags.Finance.Modules.Transactions.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ags.Finance.Modules.Reimbursements;

public static class DependencyInjection
{
    public static IServiceCollection AddReimbursementsModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContextFactory<ReimbursementsDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MigrationsHistoryTable(ReimbursementsDbContext.MigrationsHistoryTable, ReimbursementsDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddScoped<IReimbursementStore, ReimbursementStore>();
        services.AddScoped<IPersonCatalog, PersonCatalog>();
        services.AddScoped<ISharingRuleCatalog, SharingRuleCatalog>();
        services.AddScoped<IReimbursementStatements, ReimbursementStatements>();
        services.AddScoped<IReimbursementReceipts, ReimbursementReceipts>();
        services.AddScoped<ISavedFilterUsageGuard, SharingRuleFilterUsageGuard>();
        return services;
    }

    public static async Task MigrateReimbursementsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ReimbursementsDbContext>>().CreateDbContextAsync(cancellationToken);
        await using (context)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
