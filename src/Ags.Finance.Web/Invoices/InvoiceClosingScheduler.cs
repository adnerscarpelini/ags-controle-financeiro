using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Web.Backup;

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
                InvoiceClosingLog.Failed(logger, exception.GetType().Name);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

internal static partial class InvoiceClosingLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Não foi possível executar o fechamento automático de faturas ({ErrorType}).")]
    public static partial void Failed(ILogger logger, string errorType);
}
