using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record InvoiceMonthBalance(AccountingMonth Month, Money RemainingBalance, bool FullyPaid);
public sealed record CardFutureBalance(Guid CardExternalId, string CardName, Money RemainingBalance);
public sealed record InvoiceObligations(AccountingMonth SelectedMonth, IReadOnlyList<InvoiceMonthBalance> Months, IReadOnlyList<CardFutureBalance> Cards, string? Error = null);
