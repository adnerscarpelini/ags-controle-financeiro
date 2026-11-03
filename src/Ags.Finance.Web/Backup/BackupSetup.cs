using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Web.Attachments;

namespace Ags.Finance.Web.Backup;

internal static class BackupSetup
{
    public static IServiceCollection AddBackup(this IServiceCollection services, IConfiguration configuration, string connectionString, string dataDirectory)
    {
        var settings = BackupSettings.FromConfiguration(configuration);
        services.AddSingleton(settings);
        services.AddSingleton(new BackupEnvironment(connectionString, dataDirectory, BackupEnvironment.CurrentAppVersion));
        services.AddSingleton<IPostgresTools, PostgresTools>();
        if (services.Any(x => x.ServiceType == typeof(AttachmentStorageOptions))) { services.AddSingleton<AttachmentBackup>(); }
        services.AddSingleton<BackupService>();
        services.AddSingleton<BackupStatus>();
        services.AddSingleton<MaintenanceMode>();
        services.AddSingleton<RestoreCoordinator>();
        services.AddHostedService<BackupScheduler>();
        return services;
    }
}
