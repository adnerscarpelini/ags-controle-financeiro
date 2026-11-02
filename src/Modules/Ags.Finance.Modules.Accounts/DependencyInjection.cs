using Ags.Finance.Modules.Accounts.Application;
using Ags.Finance.Modules.Accounts.Application.CalculateAccountingMonth;
using Ags.Finance.Modules.Accounts.Application.ManageCardLastDigits;
using Ags.Finance.Modules.Accounts.Application.ManageCatalog;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ags.Finance.Modules.Accounts;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContextFactory<AccountsDbContext>((provider, options) => options
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable(AccountsDbContext.MigrationsHistoryTable))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddScoped<IAccountsStore, AccountsStore>();
        services.AddScoped<IAccountCatalog, AccountCatalog>();
        services.AddScoped<ICardLastDigitsCatalog, CardLastDigitsCatalog>();
        services.AddScoped<IAccountingMonthCalculator, AccountingMonthCalculator>();
        return services;
    }

    public static async Task MigrateAccountsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
