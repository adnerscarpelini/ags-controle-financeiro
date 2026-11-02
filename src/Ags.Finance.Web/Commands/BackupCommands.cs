using System.Globalization;
using System.Reflection;
using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Backup;
using Ags.Finance.Web.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ags.Finance.Web.Commands;

/// <summary>
/// Os comandos <c>backup create|list|verify|restore</c>, que rodam sem subir o servidor web. São o caminho de
/// manutenção da instalação Docker: <c>docker compose run --rm app backup create</c>.
/// </summary>
internal sealed class BackupCommands(BackupService service, BackupSettings settings, string connectionString, ITerminal terminal)
{
    public const string Usage =
        "Uso: Ags.Finance.Web backup <create | list | verify NOME | restore NOME [--force] [--yes]>";

    public static string AppVersion =>
        typeof(BackupCommands).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "desconhecida";

    public async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        switch (arguments)
        {
            case ["create"]:
                return await CreateAsync(cancellationToken);
            case ["list"]:
                return List();
            case ["verify", var name]:
                return await VerifyAsync(name, cancellationToken);
            case ["restore", var name, .. var options] when options.All(option => option is "--force" or "--yes"):
                return await RestoreAsync(name, options.Contains("--force"), options.Contains("--yes"), cancellationToken);
            default:
                terminal.WriteError(Usage);
                return UserCommands.Usage;
        }
    }

    private async Task<int> CreateAsync(CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(BackupService.KindManual, cancellationToken);
        if (!result.Succeeded)
        {
            terminal.WriteError($"Backup falhou: {result.Error}");
            return UserCommands.Failed;
        }

        var manifest = result.Package!.Manifest;
        terminal.WriteLine($"Backup concluído: {result.Package.Name}");
        terminal.WriteLine($"  PostgreSQL {manifest.PostgresServerVersion}, pg_dump {manifest.PgDumpVersion}, aplicação {manifest.AppVersion}");
        terminal.WriteLine($"  Pasta: {result.Package.Path}");
        terminal.WriteLine("  Copie esta pasta para fora da máquina/disco: o volume sozinho não protege contra a perda do computador.");
        return UserCommands.Success;
    }

    private int List()
    {
        var packages = service.List();
        if (packages.Count == 0)
        {
            terminal.WriteLine(settings.Directory is null ? "A pasta de backup não está configurada." : "Nenhum backup encontrado.");
            return UserCommands.Success;
        }

        foreach (var package in packages)
        {
            var size = package.Manifest.Files.Sum(file => file.Bytes);
            terminal.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{package.Name}  {package.Manifest.CreatedAt.UtcDateTime:yyyy-MM-dd HH:mm}Z  {package.Manifest.Kind}{(package.Manifest.Monthly ? " (mensal)" : string.Empty)}  {size / 1024} KiB"));
        }

        return UserCommands.Success;
    }

    private async Task<int> VerifyAsync(string name, CancellationToken cancellationToken)
    {
        var package = service.List().FirstOrDefault(candidate => candidate.Name == name);
        if (package is null)
        {
            terminal.WriteError($"O backup '{name}' não foi encontrado.");
            return UserCommands.Failed;
        }

        var problem = await service.VerifyAsync(package, cancellationToken);
        if (problem is not null)
        {
            terminal.WriteError(problem);
            return UserCommands.Failed;
        }

        terminal.WriteLine($"O backup {name} está íntegro.");
        return UserCommands.Success;
    }

    private async Task<int> RestoreAsync(string name, bool force, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed)
        {
            terminal.WriteLine("A restauração SUBSTITUI todos os dados atuais pelos do backup (um backup do estado atual é feito antes).");
            var answer = terminal.ReadLine($"Digite o nome do backup ({name}) para confirmar: ");
            if (answer?.Trim() != name)
            {
                terminal.WriteError("Confirmação não conferiu; nada foi alterado.");
                return UserCommands.Failed;
            }
        }

        var result = await service.RestoreAsync(name, force, cancellationToken);
        if (!result.Succeeded)
        {
            terminal.WriteError($"Restauração falhou: {result.Error}");
            return UserCommands.Failed;
        }

        // O backup pode ser de uma versão anterior: aplica as migrations que faltarem.
        await using var provider = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddIdentityModule(connectionString)
            .AddAccountsModule(connectionString)
            .AddTransactionsModule(connectionString)
            .AddInvoicesModule(connectionString)
            .AddReimbursementsModule(connectionString)
            .BuildServiceProvider();
        await DatabaseStartup.InitializeAsync(provider, connectionString, TimeSpan.FromSeconds(30), NullLogger.Instance, cancellationToken);

        terminal.WriteLine($"Restauração concluída a partir de {name}. Reinicie a aplicação: docker compose up -d app");
        return UserCommands.Success;
    }
}
