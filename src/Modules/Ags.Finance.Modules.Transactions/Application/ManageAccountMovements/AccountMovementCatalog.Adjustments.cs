using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.Modules.Transactions.Domain;

namespace Ags.Finance.Modules.Transactions.Application.ManageAccountMovements;

internal sealed partial class AccountMovementCatalog
{
    public Task<ClassificationResult> AdjustBalanceAsync(BalanceAdjustmentRequest request, CancellationToken cancellationToken = default)
        => operations.ExecuteAsync(async token =>
        {
            if (request.ConfirmationId == Guid.Empty || request.Date == DateOnly.MinValue || Math.Abs(request.CorrectBalance.Amount) > 92233720368547758.07m)
            {
                return new ClassificationResult(null, "Informe confirmação, data e saldo válidos.");
            }
            var previous = (await store.GetAsync(token)).Where(x => x.OperationExternalId == request.ConfirmationId).ToList();
            if (previous.Count > 0)
            {
                var matches = previous.Count == 1 && previous[0].Kind == Domain.AccountMovementKind.BalanceAdjustment
                    && previous[0].AccountExternalId == request.AccountExternalId && previous[0].Date == request.Date && previous[0].TargetBalance == request.CorrectBalance;
                return matches ? new ClassificationResult(request.ConfirmationId, null) : new ClassificationResult(null, "Esta confirmação já foi utilizada em outra operação.");
            }
            var account = (await accounts.GetAccountsAsync(cancellationToken: token)).FirstOrDefault(x => x.ExternalId == request.AccountExternalId);
            if (account is null) { return new ClassificationResult(null, "Selecione uma conta ativa cadastrada."); }
            if (account.OpeningDate > request.Date) { return new ClassificationResult(null, "O ajuste deve ocorrer a partir da referência do saldo inicial."); }
            var balance = await entries.GetBalanceAsync(account.ExternalId, request.Date, token);
            if (balance is null) { return new ClassificationResult(null, "Não foi possível calcular o saldo da conta."); }
            if (request.ExpectedBalance is { } expected && expected != balance.EffectiveBalance)
            {
                return new ClassificationResult(null, "O saldo mudou desde a prévia. Cancele e confira o ajuste novamente.");
            }
            var difference = request.CorrectBalance - balance.EffectiveBalance;
            if (difference.IsZero) { return new ClassificationResult(request.ConfirmationId, null); }
            var adjustment = AccountMovement.Adjustment(request.ConfirmationId, account.ExternalId, request.Date, difference, request.CorrectBalance);
            if (adjustment is null) { return new ClassificationResult(null, "A diferença ultrapassa o valor suportado."); }
            await store.AddAsync([adjustment], token);
            return new ClassificationResult(request.ConfirmationId, null);
        }, x => x.Succeeded, cancellationToken);
}
