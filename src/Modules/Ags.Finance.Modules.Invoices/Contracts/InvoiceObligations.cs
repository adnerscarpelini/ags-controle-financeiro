using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record InvoiceMonthBalance(ReferenceMonth Month, Money RemainingBalance, bool FullyPaid);
public sealed record CardFutureBalance(Guid CardExternalId, string CardName, Money RemainingBalance);
public sealed record InvoiceObligations(ReferenceMonth SelectedMonth, IReadOnlyList<InvoiceMonthBalance> Months, IReadOnlyList<CardFutureBalance> Cards, string? Error = null);
