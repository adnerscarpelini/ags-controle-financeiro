using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Transactions.Application.CreateEntry;

internal sealed partial class EntryCatalog
{
    public Task<ClassificationResult> AdjustInvoiceAsync(InvoiceAdjustmentRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (request.ConfirmationId == Guid.Empty || request.Month.Year == 0 || Math.Abs(request.CorrectTotal.Amount) > 92233720368547758.07m) { return new ClassificationResult(null, "Informe confirmação, mês e total válidos."); }
            var adjustments = await store.GetInvoiceAdjustmentsAsync(token);
            if (adjustments.FirstOrDefault(x => x.ConfirmationId == request.ConfirmationId) is { } previous)
            {
                return previous.CardExternalId == request.CardExternalId && previous.Month == request.Month && previous.CorrectTotal == request.CorrectTotal
                    ? new ClassificationResult(previous.ExternalId, null) : new ClassificationResult(null, "Esta confirmação já foi utilizada.");
            }
            var card = (await accounts.GetCreditCardsAsync(false, token)).FirstOrDefault(x => x.ExternalId == request.CardExternalId);
            if (card is null) { return new ClassificationResult(null, "Cartão não encontrado."); }
            var protectedInvoice = await invoices.RequireAdjustmentAsync(card.ExternalId, request.Month, token);
            if (protectedInvoice is not null) { return new ClassificationResult(null, protectedInvoice); }
            var generation = await GenerateRecurringAsync(new(Month: request.Month), token);
            if (!generation.Succeeded) { return new ClassificationResult(null, generation.Error); }
            // RecurringCutoff: ocorrências recorrentes com data depois dele ainda não foram cobradas pelo banco e ficam fora do total comparado.
            var gross = -(await store.GetAsync(token)).Where(x => x.CardExternalId == card.ExternalId && x.Month == request.Month
                    && !(request.RecurringCutoff is { } cutoff && x.RecurringEntryExternalId is not null && x.Date > cutoff)).Sum(x => x.Amount.Amount)
                + adjustments.Where(x => x.CardExternalId == card.ExternalId && x.Month == request.Month).Sum(x => x.ExpenseDifference.Amount);
            var current = new Money(gross);
            if (request.ExpectedTotal is { } expected && expected != current) { return new ClassificationResult(null, "O total mudou desde a prévia. Cancele e confira novamente."); }
            var difference = request.CorrectTotal - current;
            if (difference.IsZero) { return new ClassificationResult(request.ConfirmationId, null); }
            var adjustment = InvoiceAdjustment.Create(request.ConfirmationId, card.ExternalId, card.AccountExternalId, request.Month,
                DateOnly.FromDateTime(clock.GetLocalNow().DateTime), difference, request.CorrectTotal);
            if (adjustment is null) { return new ClassificationResult(null, "A diferença ultrapassa o valor suportado."); }
            await store.AddInvoiceAdjustmentAsync(adjustment, token);
            return new ClassificationResult(adjustment.ExternalId, null);
        }, x => x.Succeeded, cancellationToken);

    private async Task<IReadOnlyList<EntryDetails>> DescribeWithAdjustmentsAsync(IReadOnlyList<TransactionEntry> rows, IReadOnlyList<InvoiceAdjustment> adjustments, CancellationToken cancellationToken)
    {
        var described = await DescribeAsync(rows, cancellationToken);
        return rows.Zip(described, (entry, details) => (entry.Date, entry.Id, Source: 0, Details: details))
            .Concat(adjustments.Select(x => (x.Date, x.Id, Source: 1, Details: DescribeAdjustment(x))))
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ThenBy(x => x.Source).Select(x => x.Details).ToList();
    }

    private static EntryDetails DescribeAdjustment(InvoiceAdjustment adjustment) => new(adjustment.ExternalId, adjustment.Date, adjustment.Month,
        Guid.Empty, "Ajuste de fatura", Guid.Empty, Guid.Empty, "Ajuste de fatura", adjustment.AccountExternalId, Contracts.CategoryKind.Expense,
        -adjustment.ExpenseDifference, $"Total correto informado: {adjustment.CorrectTotal}", true, adjustment.Date, adjustment.CreatedAt, CardExternalId: adjustment.CardExternalId, IsInvoiceAdjustment: true);

    private static bool MatchesAdjustment(InvoiceAdjustment adjustment, EntryFilter filter)
        => !filter.PendingRecurringOnly && !filter.HasReimbursement && filter.IsEffective != false && (filter.Kind is null or Contracts.CategoryKind.Expense)
            && (filter.Month is null || filter.Month == adjustment.Month) && (filter.FromDate is null || adjustment.Date >= filter.FromDate)
            && (filter.ToDate is null || adjustment.Date <= filter.ToDate) && (filter.CardExternalId is null || filter.CardExternalId == adjustment.CardExternalId)
            && (filter.AccountExternalId is null || filter.AccountExternalId == adjustment.AccountExternalId) && filter.CardLastDigitsExternalId is null
            && (filter.CategoryExternalId is null || filter.CategoryExternalId == Guid.Empty) && (filter.SubcategoryExternalId is null || filter.SubcategoryExternalId == Guid.Empty)
            && (filter.CardExternalIds is not { Count: > 0 } cards || cards.Contains(adjustment.CardExternalId))
            && (filter.AccountExternalIds is not { Count: > 0 } accounts || accounts.Contains(adjustment.AccountExternalId))
            && filter.CardLastDigitsExternalIds is not { Count: > 0 } && filter.CategoryExternalIds is not { Count: > 0 }
            && filter.SubcategoryExternalIds is not { Count: > 0 } && filter.MerchantExternalIds is not { Count: > 0 }
            && (string.IsNullOrWhiteSpace(filter.Text) || (Merchant.Normalize(filter.Text) is { Length: > 0 } compact && Merchant.Normalize("Ajuste de fatura").Contains(compact, StringComparison.Ordinal))
                || SearchText.Normalize($"Total correto informado: {adjustment.CorrectTotal}").Contains(SearchText.Normalize(filter.Text.Trim()), StringComparison.Ordinal));
}
