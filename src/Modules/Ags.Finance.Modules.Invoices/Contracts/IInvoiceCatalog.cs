using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Contracts;

public sealed record InvoiceDetails(Guid CardExternalId, string CardName, AccountingMonth Month, Money Total, IReadOnlyList<EntryDetails> Entries, InvoiceStatus Status = InvoiceStatus.Open, Guid? ExternalId = null, IReadOnlyList<InvoicePaymentDetails>? Payments = null, bool HasConfirmedPayment = false, DateTimeOffset? PaidAt = null, IReadOnlyList<EntryDetails>? Forecast = null, bool ForecastIncluded = false)
{
    public IReadOnlyList<InvoicePaymentDetails> PaymentHistory => Payments ?? [];
    public Money PaymentsTotal => new(PaymentHistory.Where(x => x.Kind == InvoicePaymentKind.Payment).Sum(x => x.Amount.Amount));
    public Money CashbackTotal => new(PaymentHistory.Where(x => x.Kind == InvoicePaymentKind.Cashback).Sum(x => x.Amount.Amount));
    public Money DiscountsTotal => new(PaymentHistory.Where(x => x.Kind == InvoicePaymentKind.Discount).Sum(x => x.Amount.Amount));
    public Money RemainingBalance => Total - PaymentsTotal - CashbackTotal - DiscountsTotal;

    /// <summary>
    /// Ocorrências recorrentes do cartão com data posterior a hoje numa fatura Aberta (subconjunto de <see cref="Entries"/>).
    /// Fatura Fechada ou Paga nunca tem previstas: o total inclui todas as ocorrências.
    /// </summary>
    public IReadOnlyList<EntryDetails> ForecastEntries => Forecast ?? [];

    /// <summary>Soma das previstas, como valor positivo de fatura.</summary>
    public Money ForecastTotal => new(-ForecastEntries.Sum(x => x.Amount.Amount));

    /// <summary>Total com as previstas, venham elas somadas (<c>ForecastIncluded</c>) ou à parte.</summary>
    public Money ProjectedTotal => ForecastIncluded ? Total : Total + ForecastTotal;

    /// <summary>Total já lançado até hoje, sem as previstas: é o que o banco mostra e o que o ajuste de total compara.</summary>
    public Money LaunchedTotal => ForecastIncluded ? Total - ForecastTotal : Total;

    /// <summary>Fatura cuja única movimentação são previstas deixadas fora do total (não conta como mês na média do ano).</summary>
    public bool OnlyForecast => !ForecastIncluded && ForecastEntries.Count > 0 && ForecastEntries.Count == Entries.Count;
}

public interface IInvoiceCatalog
{
    Task<InvoiceObligations> GetObligationsAsync(AccountingMonth selectedMonth, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceDetails>> GetAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceDetails>> GetMonthAsync(AccountingMonth month, CancellationToken cancellationToken = default);
}
