using Ags.Finance.Modules.Accounts.Application;
using Ags.Finance.Modules.Accounts.Application.CalculateReferenceMonth;
using Ags.Finance.Modules.Accounts.Application.ManageBankLogos;
using Ags.Finance.Modules.Accounts.Application.ManageCardLastDigits;
using Ags.Finance.Modules.Accounts.Application.ManageCatalog;
using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Accounts.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ags.Finance.Modules.Accounts;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.TryAddSingleton<IFinancialOperation, FinancialOperation>();
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContextFactory<AccountsDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MigrationsHistoryTable(AccountsDbContext.MigrationsHistoryTable, AccountsDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddScoped<IAccountsStore, AccountsStore>();
        services.AddScoped<IAccountCatalog, AccountCatalog>();
        services.AddSingleton<ILogoProcessor, LogoProcessor>();
        services.AddScoped<IBankLogoCatalog, BankLogoCatalog>();
        services.AddScoped<ICardLastDigitsCatalog, CardLastDigitsCatalog>();
        services.AddScoped<IReferenceMonthCalculator, ReferenceMonthCalculator>();
        return services;
    }

    public static async Task MigrateAccountsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AccountsDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
