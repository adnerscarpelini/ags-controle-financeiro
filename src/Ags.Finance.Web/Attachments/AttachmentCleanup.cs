using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Web.Backup;
using Ags.Finance.Web.Hosting;

namespace Ags.Finance.Web.Attachments;

internal sealed class AttachmentCleanup(IServiceScopeFactory scopes, MaintenanceMode maintenance, TimeProvider clock, ILogger<AttachmentCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromDays(1), clock, stoppingToken);
                if (maintenance.IsActive) { continue; }
                await using var scope = scopes.CreateAsyncScope();
                try { await scope.ServiceProvider.GetRequiredService<IAttachmentCatalog>().PurgeAsync(stoppingToken); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Npgsql.NpgsqlException)
                {
                    // Repete na próxima execução diária.
                    OperationLog.Failed(logger, "Limpeza da lixeira de anexos", exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
