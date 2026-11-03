using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Attachments;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Preferences;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Attachments;
using Ags.Finance.Web.Persistence;

namespace Ags.Finance.Web.Commands;

internal static class AttachmentCommandRunner
{
    public static async Task<int> RunAsync(string[] arguments, IConfiguration configuration, ITerminal terminal, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = DatabaseStartup.GetConnectionString(configuration);
            var data = DataDirectoryResolver.Resolve(configuration["DataDirectory"], OperatingSystem.IsWindows());
            var options = AttachmentSetup.ReadOptions(configuration, data);
            await using var provider = new ServiceCollection().AddAttachmentsModule(connection, options).AddAccountsModule(connection)
                .AddTransactionsModule(connection).AddInvoicesModule(connection).AddPreferencesModule(connection).BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var inventory = ActivatorUtilities.CreateInstance<AttachmentInventory>(scope.ServiceProvider);
            var export = ActivatorUtilities.CreateInstance<AttachmentExport>(scope.ServiceProvider);
            return await new AttachmentCommands(inventory, export, terminal).RunAsync(arguments, cancellationToken);
        }
        catch (InvalidOperationException exception) { terminal.WriteError(exception.Message); return UserCommands.Failed; }
        catch (Npgsql.NpgsqlException) { terminal.WriteError("Não foi possível consultar os metadados dos anexos. Confira o PostgreSQL e inicie a aplicação para aplicar migrations pendentes."); return UserCommands.Failed; }
    }
}
