using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Invoices.Contracts;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Invoices.Application.AnticipateInstallments;

internal sealed class InvoiceAnticipationCatalog(IEntryCatalog entries, IInvoicePaymentCatalog payments, IInvoiceStore store,
    IReferenceMonthCalculator calendar, TimeProvider clock, IFinancialOperation operations) : IInvoiceAnticipationCatalog
{
    public Task<InvoiceResult> AnticipateAsync(AnticipateInstallmentsRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(token => AnticipateCoreAsync(request, token), x => x.Succeeded, cancellationToken);

    private async Task<InvoiceResult> AnticipateCoreAsync(AnticipateInstallmentsRequest request, CancellationToken cancellationToken)
    {
        if (request.ConfirmationId == Guid.Empty || request.Count is < 1 or > 48 || request.Discount.IsNegative) { return new("Informe confirmação, quantidade positiva e desconto não negativo."); }
        var previous = (await store.GetAnticipationsAsync(cancellationToken)).FirstOrDefault(x => x.ConfirmationId == request.ConfirmationId);
        if (previous is not null)
        {
            return previous.EntryExternalId == request.EntryExternalId && previous.Count == request.Count && previous.Discount == request.Discount
                && (request.ExpectedMonth is null || previous.Month == request.ExpectedMonth) ? new(null) : new("Esta confirmação já foi utilizada.");
        }
        var all = await entries.GetAsync(cancellationToken);
        var selected = all.FirstOrDefault(x => x.ExternalId == request.EntryExternalId);
        if (selected?.InstallmentPurchaseExternalId is not { } purchase || selected.CardExternalId is not { } card) { return new("Selecione uma parcela de compra no cartão."); }
        var date = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var suggestion = await calendar.CalculateAsync(date, card, cancellationToken);
        if (suggestion.Month is not { } month) { return new(suggestion.Error); }
        if (request.ExpectedMonth is { } expected && expected != month) { return new("A fatura atual mudou. Reabra a antecipação para conferir."); }
        var future = all.Where(x => x.InstallmentPurchaseExternalId == purchase && x.Month > month).OrderByDescending(x => x.InstallmentNumber).ToList();
        if (request.Count > future.Count) { return new("A quantidade excede as parcelas futuras disponíveis."); }
        var moving = future.Take(request.Count).ToList();
        var total = new Money(-moving.Sum(x => x.Amount.Amount));
        var operation = InstallmentAnticipation.Create(request.ConfirmationId, selected.ExternalId, purchase, card, month, request.Count, total, request.Discount);
        if (operation is null) { return new("O desconto deve ser menor que o valor das parcelas antecipadas."); }
        foreach (var entry in moving)
        {
            var changed = await entries.EditAsync(entry.ExternalId, new(entry.Date, entry.MerchantExternalId, entry.SubcategoryExternalId, entry.AccountExternalId,
                new Money(Math.Abs(entry.Amount.Amount)), entry.Details, month, entry.CardExternalId, entry.CardLastDigitsExternalId, entry.Reimbursement), EntryChangeScope.OnlyThis, cancellationToken);
            if (!changed.Succeeded) { return new(changed.Error); }
        }
        if (!request.Discount.IsZero)
        {
            var allowance = await payments.SaveAllowanceAsync(null, new(request.ConfirmationId, card, month, date, request.Discount, Contracts.InvoicePaymentKind.Discount,
                $"Antecipação de {request.Count} parcelas de {selected.MerchantName} ({selected.Date:dd/MM/yyyy})"), cancellationToken);
            if (!allowance.Succeeded) { return new(allowance.Error); }
        }
        operation.SetEntries(moving.Select(x => x.ExternalId));
        await store.AddAnticipationAsync(operation, cancellationToken);
        return new(null);
    }
}
