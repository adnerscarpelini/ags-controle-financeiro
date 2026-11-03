using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Reimbursements.Application.ManagePeople;

internal sealed class ReimbursementWriteGuard(IReimbursementStore store) : IReimbursementWriteGuard
{
    public async Task<string?> RequirePersonAsync(Guid personExternalId, CancellationToken cancellationToken = default)
        => (await store.GetPeopleAsync(cancellationToken)).Any(x => x.ExternalId == personExternalId) ? null : "Selecione uma pessoa cadastrada para o reembolso.";

    public async Task<string?> RequireEntryChangeAsync(Guid entryExternalId, bool changesProtectedData, CancellationToken cancellationToken = default)
    {
        if (await store.IsIncomeOfReceiptAsync(entryExternalId, cancellationToken))
        {
            return "Este lançamento foi gerado por um recebimento de reembolso. Para corrigi-lo, desfaça o recebimento em Reembolsos.";
        }
        return changesProtectedData && await store.IsReceivedExpenseAsync(entryExternalId, cancellationToken)
            ? "Esta despesa tem reembolso recebido. Para alterar valor, tipo, pessoa ou percentual, ou excluí-la, desfaça o recebimento em Reembolsos."
            : null;
    }
}
