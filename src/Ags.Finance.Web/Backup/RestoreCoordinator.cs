using Ags.Finance.Web.Hosting;
using Ags.Finance.Web.Persistence;

namespace Ags.Finance.Web.Backup;

/// <summary>
/// Restauração disparada pela tela: entra em manutenção, restaura (encerrando as conexões da própria aplicação),
/// aplica migrations e reinicia a aplicação para recarregar certificado e chaves. Em falha, volta ao normal.
/// </summary>
internal sealed class RestoreCoordinator(
    BackupService backups,
    MaintenanceMode maintenance,
    BackupStatus status,
    BackupEnvironment environment,
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    TimeProvider clock,
    ILogger<RestoreCoordinator> logger)
{
    /// <summary>Tempo para requisições em andamento terminarem antes de a restauração começar.</summary>
    public TimeSpan Settle { get; init; } = TimeSpan.FromSeconds(3);

    public async Task<BackupResult> RestoreAsync(string name, CancellationToken cancellationToken)
    {
        if (!maintenance.TryEnter())
        {
            return BackupResult.Failure("Já existe uma restauração em andamento.");
        }

        var finished = false;
        try
        {
            await Task.Delay(Settle, clock, cancellationToken);
            var result = await backups.RestoreAsync(name, forceDisconnect: true, cancellationToken);
            if (!result.Succeeded)
            {
                return result;
            }

            finished = true;
            try
            {
                var timeout = TimeSpan.FromSeconds(60);
                await DatabaseStartup.InitializeAsync(services, environment.ConnectionString, timeout, logger, cancellationToken);
            }
            catch (Exception exception) when (exception is InvalidOperationException or Npgsql.NpgsqlException)
            {
                // Restaurado, mas as migrations falharam: a reinicialização repete o processo.
                OperationLog.Failed(logger, "Migrations após a restauração", exception);
                status.Record(BackupResult.Failure("migrations após a restauração"), clock.GetUtcNow());
            }

            // Reiniciar recarrega o certificado, as chaves do Data Protection e as conexões; o Compose sobe de novo.
            lifetime.StopApplication();
            return result;
        }
        finally
        {
            if (!finished)
            {
                maintenance.Exit();
            }
        }
    }
}
