using Ags.Finance.Modules.Attachments.Application;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Attachments.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Ags.Finance.Modules.Attachments;

public static class DependencyInjection
{
    public static IServiceCollection AddAttachmentsModule(this IServiceCollection services, string connectionString, AttachmentStorageOptions options)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<AuditTimestampsInterceptor>();
        services.AddDbContextFactory<AttachmentsDbContext>((provider, builder) => builder
            .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.MigrationsHistoryTable(AttachmentsDbContext.MigrationsHistoryTable, AttachmentsDbContext.Schema))
            .AddInterceptors(provider.GetRequiredService<AuditTimestampsInterceptor>()));
        services.AddScoped<IAttachmentStore, AttachmentStore>();
        services.AddScoped<IAttachmentFiles, AttachmentFiles>();
        services.AddScoped<IAttachmentCatalog, AttachmentCatalog>();
        return services;
    }
    public static async Task MigrateAttachmentsModuleAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AttachmentsDbContext>>().CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
