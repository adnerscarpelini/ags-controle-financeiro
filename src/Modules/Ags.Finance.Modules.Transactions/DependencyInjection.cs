using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Application.CreateEntry;
using Ags.Finance.Modules.Transactions.Application.ManageClassifications;
using Ags.Finance.Modules.Transactions.Application.ManageMerchants;
using Ags.Finance.Modules.Transactions.Application.TransferAccounts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ags.Finance.Modules.Transactions;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionsModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContextFactory<TransactionsDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MigrationsHistoryTable(TransactionsDbContext.MigrationsHistoryTable, TransactionsDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddScoped<IClassificationStore, ClassificationStore>();
        services.AddScoped<IClassificationCatalog, ClassificationCatalog>();
        services.AddScoped<IMerchantStore, MerchantStore>();
        services.AddScoped<IMerchantCatalog, MerchantCatalog>();
        services.AddScoped<IAccountMovementStore, AccountMovementStore>();
        services.AddScoped<IAccountMovementCatalog, AccountMovementCatalog>();
        services.AddScoped<IEntryStore, EntryStore>();
        services.AddScoped<IEntryCatalog, EntryCatalog>();
        services.TryAddScoped<IInvoiceWriteGuard, OpenInvoiceWriteGuard>();
        return services;
    }

    public static async Task MigrateTransactionsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
