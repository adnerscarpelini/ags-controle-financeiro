using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public enum InvoiceStatus { Open, Closed, Paid }
public sealed record InvoiceResult(string? Error)
{
    public bool Succeeded => Error is null;
}

public interface IInvoiceLifecycle
{
    Task<InvoiceResult> CloseAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<InvoiceResult> ReopenAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken = default);
    Task<int> CloseDueAsync(CancellationToken cancellationToken = default);
}
