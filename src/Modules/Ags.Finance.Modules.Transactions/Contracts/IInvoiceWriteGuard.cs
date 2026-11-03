using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Contracts;

public interface IInvoiceWriteGuard
{
    Task<bool> IsAnticipatedAsync(Guid entryExternalId, CancellationToken cancellationToken = default);
    Task<string?> RequireAdjustmentAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<string?> RequireOpenAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<string?> RequireRecurringAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default);

    /// <summary>Mesma decisão de <see cref="RequireRecurringAsync"/>, somente leitura: não registra a fatura que ainda não existe (#77).</summary>
    Task<string?> CheckRecurringAsync(Guid cardExternalId, AccountingMonth month, DateTimeOffset plannedAt, CancellationToken cancellationToken = default);
}
