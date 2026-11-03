using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record InvoiceDetails(Guid CardExternalId, string CardName, AccountingMonth Month, Money Total, IReadOnlyList<EntryDetails> Entries, InvoiceStatus Status = InvoiceStatus.Open, Guid? ExternalId = null, IReadOnlyList<InvoicePaymentDetails>? Payments = null, bool HasConfirmedPayment = false, DateTimeOffset? PaidAt = null)
{
    public IReadOnlyList<InvoicePaymentDetails> PaymentHistory => Payments ?? [];
    public Money PaymentsTotal => new(PaymentHistory.Where(x => x.Kind == InvoicePaymentKind.Payment).Sum(x => x.Amount.Amount));
    public Money CashbackTotal => new(PaymentHistory.Where(x => x.Kind == InvoicePaymentKind.Cashback).Sum(x => x.Amount.Amount));
    public Money DiscountsTotal => new(PaymentHistory.Where(x => x.Kind == InvoicePaymentKind.Discount).Sum(x => x.Amount.Amount));
    public Money RemainingBalance => Total - PaymentsTotal - CashbackTotal - DiscountsTotal;
}

public interface IInvoiceCatalog
{
    Task<InvoiceObligations> GetObligationsAsync(AccountingMonth selectedMonth, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceDetails>> GetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceDetails>> GetMonthAsync(AccountingMonth month, CancellationToken cancellationToken = default);
}
