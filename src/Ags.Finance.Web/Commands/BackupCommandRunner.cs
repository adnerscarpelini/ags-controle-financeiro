using Ags.Finance.Web.Backup;
using Ags.Finance.Web.Persistence;
using Microsoft.Extensions.Logging;

namespace Ags.Finance.Web.Commands;

/// <summary>Monta o necessário para os comandos <c>backup ...</c> e os executa sem subir o servidor web.</summary>
internal static class BackupCommandRunner
{
    public static async Task<int> RunAsync(
        string[] arguments,
        IConfiguration configuration,
        ITerminal terminal,
        CancellationToken cancellationToken = default)
    {
        string connectionString;
        BackupSettings settings;
        try
        {
            connectionString = DatabaseStartup.GetConnectionString(configuration);
            settings = BackupSettings.FromConfiguration(configuration);
        }
        catch (InvalidOperationException exception)
        {
            terminal.WriteError(exception.Message);
            return UserCommands.Failed;
        }

        var dataDirectory = DataDirectoryResolver.Resolve(configuration["DataDirectory"], OperatingSystem.IsWindows());
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
        var service = new BackupService(
            settings,
            new BackupEnvironment(connectionString, dataDirectory, BackupCommands.AppVersion),
            new PostgresTools(settings),
            TimeProvider.System,
            loggerFactory.CreateLogger<BackupService>());

        return await new BackupCommands(service, settings, connectionString, terminal).RunAsync(arguments, cancellationToken);
    }
}
