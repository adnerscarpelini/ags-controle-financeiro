using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public enum InvoicePaymentKind { Payment, Cashback, Discount }
public sealed record AllowanceRequest(Guid ConfirmationId, Guid CardExternalId, AccountingMonth Month, DateOnly Date, Money Amount, InvoicePaymentKind Kind, string Details = "");
public sealed record InvoicePaymentDetails(Guid ExternalId, Guid ConfirmationId, Guid CardExternalId, AccountingMonth Month, DateOnly Date, Money Amount, InvoicePaymentKind Kind, Guid? AccountExternalId, string Details, DateTimeOffset ConfirmedAt);
public sealed record InvoicePaymentResult(Guid? ExternalId, string? Error)
{
    public bool Succeeded => Error is null;
}

public interface IInvoicePaymentCatalog
{
    Task<InvoicePaymentResult> SaveAllowanceAsync(Guid? externalId, AllowanceRequest request, CancellationToken cancellationToken = default);
    Task<InvoiceResult> DeleteAllowanceAsync(Guid externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoicePaymentDetails>> GetHistoryAsync(Guid? cardExternalId = null, int? year = null, InvoicePaymentKind? kind = null, CancellationToken cancellationToken = default);
}
