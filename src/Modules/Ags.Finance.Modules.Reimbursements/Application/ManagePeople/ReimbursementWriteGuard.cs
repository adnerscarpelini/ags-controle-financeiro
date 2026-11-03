using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Reimbursements.Application.ManagePeople;

internal sealed class ReimbursementWriteGuard(IReimbursementStore store) : IReimbursementWriteGuard
{
    public async Task<string?> RequirePersonAsync(Guid personExternalId, CancellationToken cancellationToken = default)
        => (await store.GetPeopleAsync(cancellationToken)).Any(x => x.ExternalId == personExternalId) ? null : "Selecione uma pessoa cadastrada para o reembolso.";
}
