using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application.ManageAccountMovements;

internal sealed partial class AccountMovementCatalog
{
    public Task<ClassificationResult> RecordInvoiceDebitAsync(InvoiceDebitRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var debit = AccountMovement.InvoiceDebit(request.PaymentExternalId, request.AccountExternalId, request.Date, request.Amount);
            if (debit is null) { return new ClassificationResult(null, "Informe pagamento, conta, data e valor positivo válidos."); }
            var previous = (await store.GetAsync(token)).Where(x => x.OperationExternalId == request.PaymentExternalId).ToList();
            if (previous.Count > 0)
            {
                var same = previous.Count == 1 && previous[0].Kind == Domain.AccountMovementKind.InvoicePayment && previous[0].AccountExternalId == debit.AccountExternalId && previous[0].Date == debit.Date && previous[0].Amount == debit.Amount;
                return same ? new ClassificationResult(previous[0].ExternalId, null) : new ClassificationResult(null, "Este pagamento já tem outra movimentação associada.");
            }
            var account = (await accounts.GetAccountsAsync(cancellationToken: token)).FirstOrDefault(x => x.ExternalId == request.AccountExternalId);
            if (account?.Kind != AccountKind.Checking) { return new ClassificationResult(null, "Selecione uma conta corrente ativa para o débito."); }
            await store.AddAsync([debit], token);
            return new ClassificationResult(debit.ExternalId, null);
        }, x => x.Succeeded, cancellationToken);
}
