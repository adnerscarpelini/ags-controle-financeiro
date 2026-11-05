using Ags.Finance.Modules.Accounts.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application.ManageAccountMovements;

internal sealed partial class AccountMovementCatalog(IAccountMovementStore store, IAccountCatalog accounts, IEntryCatalog entries, IFinancialOperation operations) : IAccountMovementCatalog
{
    public Task<ClassificationResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            var pair = AccountMovement.Transfer(request.ConfirmationId, request.SourceAccountExternalId, request.DestinationAccountExternalId, request.Date, request.Amount);
            if (pair is null) { return new ClassificationResult(null, "Informe contas distintas, data e valor positivo válidos."); }
            var previous = (await store.GetAsync(token)).Where(x => x.OperationExternalId == request.ConfirmationId).ToList();
            if (previous.Count > 0)
            {
                var matches = previous.Count == 2 && pair.All(x => previous.Any(y => y.AccountExternalId == x.AccountExternalId && y.Date == x.Date && y.Amount == x.Amount && y.Kind == x.Kind));
                return matches ? new ClassificationResult(request.ConfirmationId, null) : new ClassificationResult(null, "Esta confirmação já foi utilizada em outra operação.");
            }
            var error = await TransferRules.CheckAsync(accounts, request.SourceAccountExternalId, request.DestinationAccountExternalId, request.Date, request.Amount, token);
            if (error is not null) { return new ClassificationResult(null, error); }
            await store.AddAsync(pair, token);
            return new ClassificationResult(request.ConfirmationId, null);
        }, x => x.Succeeded, cancellationToken);

    public Task<IReadOnlyList<AccountMovementDetails>> GetAsync(Guid? accountExternalId = null, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync<IReadOnlyList<AccountMovementDetails>>(async token => (await store.GetAsync(token))
            .Where(x => accountExternalId is null || x.AccountExternalId == accountExternalId)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Select(x => new AccountMovementDetails(x.ExternalId, x.OperationExternalId, x.AccountExternalId, x.Date, x.Amount, (Contracts.AccountMovementKind)x.Kind, x.CreatedAt, x.Kind == Domain.AccountMovementKind.BalanceAdjustment ? x.TargetBalance : null)).ToList(), _ => true, cancellationToken);
}
