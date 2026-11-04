using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Web.Backup;
using Ags.Finance.Web.Hosting;

namespace Ags.Finance.Web.Invoices;

internal sealed class InvoiceClosingScheduler(IServiceScopeFactory scopes, TimeProvider clock, MaintenanceMode maintenance, ILogger<InvoiceClosingScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try
            {
                if (maintenance.IsActive) { continue; }
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IInvoiceLifecycle>().CloseDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) when (exception is Npgsql.NpgsqlException or System.Transactions.TransactionException)
            {
                OperationLog.Failed(logger, "Fechamento automático de faturas", exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
