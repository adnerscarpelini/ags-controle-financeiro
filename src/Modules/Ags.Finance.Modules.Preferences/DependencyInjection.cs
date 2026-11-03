using Ags.Finance.Modules.Preferences.Application;
using Ags.Finance.Modules.Preferences.Application.ManagePreferences;
using Ags.Finance.Modules.Preferences.Contracts;
using Ags.Finance.Modules.Preferences.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ags.Finance.Modules.Preferences;

public static class DependencyInjection
{
    public static IServiceCollection AddPreferencesModule(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContextFactory<PreferencesDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MigrationsHistoryTable(PreferencesDbContext.MigrationsHistoryTable, PreferencesDbContext.Schema))
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));

        services.AddScoped<IPreferencesStore, PreferencesStore>();
        services.AddScoped<IPreferences, PreferencesCatalog>();
        return services;
    }

    public static async Task MigratePreferencesModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<PreferencesDbContext>>().CreateDbContextAsync(cancellationToken);
        await using (context)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
