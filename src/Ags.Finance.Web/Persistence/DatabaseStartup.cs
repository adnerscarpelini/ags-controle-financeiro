using Ags.Finance.Modules.Accounts;
using Ags.Finance.Modules.Identity;
using Ags.Finance.Modules.Invoices;
using Ags.Finance.Modules.Reimbursements;
using Ags.Finance.Modules.Transactions;
using Ags.Finance.Web.Hosting;
using Npgsql;

namespace Ags.Finance.Web.Persistence;

internal static class DatabaseStartup
{
    public const string ConnectionStringName = "Finance";
    public const string ReadyTimeoutKey = "Database:ReadyTimeoutSeconds";
    public const int DefaultReadyTimeoutSeconds = 60;

    private const string ConnectionStringKey = "ConnectionStrings:" + ConnectionStringName;
    private static readonly TimeSpan s_retryInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Lê a connection string do PostgreSQL (<c>ConnectionStrings:Finance</c>, variável <c>ConnectionStrings__Finance</c>).
    /// As mensagens de erro nunca repetem o valor, que contém a senha.
    /// </summary>
    /// <exception cref="InvalidOperationException">Ausente ou sem servidor, banco e usuário.</exception>
    public static string GetConnectionString(IConfiguration configuration)
    {
        var raw = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                $"A conexão com o PostgreSQL não foi configurada. Defina {ConnectionStringKey.Replace(':', '_')} " +
                "(variável de ambiente ConnectionStrings__Finance) com servidor, banco, usuário e senha.");
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(raw);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new InvalidOperationException(
                $"A connection string {ConnectionStringKey} do PostgreSQL é inválida (o valor não é exibido por conter a senha).");
        }

        if (string.IsNullOrWhiteSpace(builder.Host) || string.IsNullOrWhiteSpace(builder.Database) || string.IsNullOrWhiteSpace(builder.Username))
        {
            throw new InvalidOperationException(
                $"A connection string {ConnectionStringKey} precisa informar Host, Database e Username.");
        }

        return builder.ConnectionString;
    }

    /// <summary>Segundos que a aplicação espera o PostgreSQL ficar pronto antes de desistir.</summary>
    public static TimeSpan GetReadyTimeout(IConfiguration configuration)
    {
        var raw = configuration[ReadyTimeoutKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TimeSpan.FromSeconds(DefaultReadyTimeoutSeconds);
        }

        if (!int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds) || seconds < 1)
        {
            throw new InvalidOperationException($"{ReadyTimeoutKey} deve ser um número inteiro de segundos maior que zero, mas veio '{raw}'.");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Espera o PostgreSQL aceitar conexões e aplica as migrations pendentes de cada módulo com persistência.</summary>
    /// <exception cref="InvalidOperationException">O banco não ficou disponível ou recusou as credenciais.</exception>
    public static async Task InitializeAsync(
        IServiceProvider services,
        string connectionString,
        TimeSpan readyTimeout,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await WaitUntilReadyAsync(connectionString, readyTimeout, logger, cancellationToken);

        await services.MigrateIdentityModuleAsync(cancellationToken);
        await services.MigrateAccountsModuleAsync(cancellationToken);
        await services.MigrateTransactionsModuleAsync(cancellationToken);
        await services.MigrateInvoicesModuleAsync(cancellationToken);
        await services.MigrateReimbursementsModuleAsync(cancellationToken);
    }

    /// <summary>
    /// Tenta conectar até o prazo. Credenciais recusadas e banco inexistente falham de imediato: esperar não resolve.
    /// </summary>
    public static async Task WaitUntilReadyAsync(
        string connectionString,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var target = Describe(connectionString);
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (true)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return;
            }
            catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.InvalidPassword
                or PostgresErrorCodes.InvalidAuthorizationSpecification
                or PostgresErrorCodes.InvalidCatalogName)
            {
                throw new InvalidOperationException(
                    $"O PostgreSQL em {target} recusou a conexão ({exception.SqlState}). Confira usuário, senha e banco configurados.");
            }
            catch (Exception exception) when (exception is NpgsqlException or System.Net.Sockets.SocketException or TimeoutException)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new InvalidOperationException(
                        $"O PostgreSQL em {target} não ficou disponível em {timeout.TotalSeconds:0} s. Confira se o serviço do banco está de pé e acessível.",
                        exception);
                }

                StartupLog.WaitingForDatabase(logger, target);
                await Task.Delay(s_retryInterval, cancellationToken);
            }
        }
    }

    /// <summary>Servidor, porta e banco para mensagens e logs, sem usuário nem senha.</summary>
    private static string Describe(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"{builder.Host}:{builder.Port}/{builder.Database}";
    }
}
