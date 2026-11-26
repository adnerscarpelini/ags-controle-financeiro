using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using CashbackDestination = Ags.Finance.Modules.Invoices.Contracts.CashbackDestination;
using InvoiceStatus = Ags.Finance.Modules.Invoices.Contracts.InvoiceStatus;

namespace Ags.Finance.Modules.Invoices.Application.ReceiveCashback;

/// <summary>
/// Painel e recebimento de cashback. Segue o modelo dos reembolsos: Previsto e A receber são derivados das faturas;
/// só o recebimento é gravado, junto com a receita (conta corrente) ou o abatimento (fatura Aberta) na mesma operação.
/// </summary>
internal sealed partial class CashbackCatalog(IInvoiceStore store, IInvoiceCatalog invoices, IAccountCatalog accounts, IEntryCatalog entries,
    IClassificationCatalog classifications, IMerchantCatalog merchants, IInvoiceWriteGuard guard, IFinancialOperation operations) : ICashbackCatalog
{
    internal const string CategoryName = "Cashback";
    internal const string SubcategoryName = "Cashback recebido";

    public Task<IReadOnlyList<CashbackItem>> GetMonthAsync(ReferenceMonth month, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (month.Year == 0) { return (IReadOnlyList<CashbackItem>)[]; }
            var receipts = (await store.GetCashbackReceiptsAsync(token)).ToDictionary(x => x.InvoiceExternalId);
            var months = (await store.GetAsync(token)).ToDictionary(x => x.ExternalId, x => x.Month);
            return (IReadOnlyList<CashbackItem>)(await invoices.GetMonthAsync(month, token))
                .Select(invoice => (Invoice: invoice, Receipt: invoice.ExternalId is { } id ? receipts.GetValueOrDefault(id) : null))
                .Where(x => x.Receipt is not null || x.Invoice.ExpectedCashback.Amount > 0)
                .Select(x => new CashbackItem(x.Invoice.CardExternalId, x.Invoice.CardName, x.Invoice.Month, x.Invoice.Status, x.Invoice.ExpectedCashback,
                    x.Receipt is not null ? CashbackStatus.Received : x.Invoice.Status == InvoiceStatus.Paid ? CashbackStatus.ToReceive : CashbackStatus.Expected,
                    x.Receipt is { } receipt ? Describe(receipt, months) : null))
                .ToList();
        }, _ => true, cancellationToken);

    public Task<IReadOnlyList<CashbackHistoryItem>> GetHistoryAsync(int year, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var states = (await store.GetAsync(token)).ToDictionary(x => x.ExternalId);
            var cards = (await accounts.GetCreditCardsAsync(false, token)).ToDictionary(x => x.ExternalId, x => x.Name);
            string Card(Guid card) => cards.GetValueOrDefault(card) ?? "Cartão";
            var received = (await store.GetCashbackReceiptsAsync(token)).Where(x => x.Date.Year == year && !x.Received.IsZero)
                .Select(x => (Receipt: x, Source: states[x.InvoiceExternalId]))
                .Select(x => new CashbackHistoryItem(x.Source.CardExternalId, Card(x.Source.CardExternalId), x.Source.Month, x.Receipt.Date, x.Receipt.Received,
                    (CashbackDestination)x.Receipt.Destination, TargetMonth: x.Receipt.TargetInvoiceExternalId is { } target ? states[target].Month : null));
            // Cashback lançado à mão antes do painel: entra no histórico como recebido, abatido na própria fatura.
            var manual = (await store.GetPaymentsAsync(token)).Where(x => x.Kind == Domain.InvoicePaymentKind.Cashback && x.CashbackReceiptExternalId is null && x.Date.Year == year)
                .Select(x => (Payment: x, Source: states[x.InvoiceExternalId]))
                .Select(x => new CashbackHistoryItem(x.Source.CardExternalId, Card(x.Source.CardExternalId), x.Source.Month, x.Payment.Date, x.Payment.Amount,
                    CashbackDestination.Invoice, IsManual: true, TargetMonth: x.Source.Month));
            return (IReadOnlyList<CashbackHistoryItem>)received.Concat(manual).OrderByDescending(x => x.Date).ThenBy(x => x.CardName, StringComparer.CurrentCulture).ToList();
        }, _ => true, cancellationToken);

    public Task<IReadOnlyList<ReferenceMonth>> GetTargetInvoicesAsync(Guid cardExternalId, DateOnly date, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if ((await accounts.GetCreditCardsAsync(false, token)).All(x => x.ExternalId != cardExternalId)) { return (IReadOnlyList<ReferenceMonth>)[]; }
            var selection = await invoices.GetSelectableInvoicesAsync(cardExternalId, date, cancellationToken: token);
            return (IReadOnlyList<ReferenceMonth>)selection.Invoices
                .Where(x => x.Status == InvoiceStatus.Open && (selection.SuggestedMonth is not { } first || x.Month >= first))
                .Select(x => x.Month).ToList();
        }, _ => true, cancellationToken);

    private static CashbackReceiptDetails Describe(CashbackReceipt receipt, Dictionary<Guid, ReferenceMonth> months)
        => new(receipt.ExternalId, receipt.Date, receipt.Expected, receipt.Received, (CashbackDestination)receipt.Destination, receipt.AccountExternalId,
            receipt.TargetInvoiceExternalId is { } target ? months[target] : null);
}
