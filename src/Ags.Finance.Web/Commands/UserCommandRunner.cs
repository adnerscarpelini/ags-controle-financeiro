using Ags.Finance.Modules.Identity;
using Ags.Finance.Web.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Web.Commands;

/// <summary>
/// Executa <c>user create</c> e <c>user reset-password</c> sem subir o servidor web: monta só o módulo Identity
/// sobre o mesmo banco do serviço e termina com um código de saída que os scripts de instalação interpretam.
/// </summary>
internal static class UserCommandRunner
{
    public const string Usage = "Uso: Ags.Finance.Web user <create|reset-password>";

    /// <param name="arguments">Os argumentos depois de <c>user</c>.</param>
    public static async Task<int> RunAsync(
        string[] arguments,
        IConfiguration configuration,
        ITerminal terminal,
        CancellationToken cancellationToken = default)
    {
        if (arguments is not [("create" or "reset-password") and var command])
        {
            terminal.WriteError(Usage);
            return UserCommands.Usage;
        }

        var dataDirectory = DataDirectoryResolver.Resolve(
            configuration["DataDirectory"],
            DataDirectoryResolver.IsRunningAsService(),
            OperatingSystem.IsWindows());
        var connectionString = DatabaseStartup.PrepareConnectionString(dataDirectory);

        await using var provider = new ServiceCollection()
            .AddIdentityModule(connectionString)
            .BuildServiceProvider();
        await provider.MigrateIdentityModuleAsync(cancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var commands = new UserCommands(
            scope.ServiceProvider.GetRequiredService<Modules.Identity.Contracts.ICreateUser>(),
            scope.ServiceProvider.GetRequiredService<Modules.Identity.Contracts.IResetPassword>(),
            scope.ServiceProvider.GetRequiredService<Modules.Identity.Contracts.IUserExists>(),
            terminal);

        return command == "create"
            ? await commands.CreateAsync(cancellationToken)
            : await commands.ResetPasswordAsync(cancellationToken);
    }

    /// <summary>A configuração do comando: <c>appsettings.json</c> ao lado do executável, variáveis de ambiente e a linha de comando.</summary>
    public static IConfiguration BuildConfiguration(string[] arguments)
    {
        return new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(arguments)
            .Build();
    }
}
