using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reports.Contracts;

/// <summary>Uma linha por cartão (ou o total) para o mês consultado. <see cref="Invoice"/> é o total da fatura exibido em Faturas, ajustes (#52) incluídos uma vez.</summary>
public sealed record CardInvoiceLine(Guid CardExternalId, string CardName, string Color, Guid BankExternalId, string BankName, Guid? BankLogoVersion, bool IsArchived,
    Money Invoice, Money Allowances, Money Paid, Money Open, Money PreviousMonth, Money YearAverage, Money OpenFromNowOn);

public sealed record CardInvoiceAmount(Guid CardExternalId, Money Total);

/// <summary>Uma coluna do gráfico empilhado: a fatura de cada cartão no mês, inclusive meses futuros com parcelas já lançadas.</summary>
public sealed record InvoiceMonthColumn(AccountingMonth Month, IReadOnlyList<CardInvoiceAmount> Cards)
{
    public Money Total => new(Cards.Sum(x => x.Total.Amount));
}

public sealed record InvoiceSeries(Guid CardExternalId, string CardName, string Color);

public sealed record InvoiceReport(AccountingMonth Month, IReadOnlyList<CardInvoiceLine> Cards, CardInvoiceLine Total, IReadOnlyList<InvoiceSeries> Series, IReadOnlyList<InvoiceMonthColumn> Months);

public interface IInvoiceReports
{
    Task<InvoiceReport> GetAsync(AccountingMonth month, CancellationToken cancellationToken = default);
}
