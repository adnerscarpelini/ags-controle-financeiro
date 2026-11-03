using Ags.Finance.Modules.Transactions.Contracts;

namespace Ags.Finance.Modules.Reimbursements.Application.ManageSharingRules;

/// <summary>Impede excluir um filtro salvo que alguma regra de rateio ainda usa.</summary>
internal sealed class SharingRuleFilterUsageGuard(IReimbursementStore store) : ISavedFilterUsageGuard
{
    public async Task<string?> FindUsageAsync(Guid savedFilterExternalId, CancellationToken cancellationToken = default)
    {
        var rules = (await store.GetRulesAsync(cancellationToken)).Where(x => x.SavedFilterExternalId == savedFilterExternalId).ToList();
        if (rules.Count == 0) { return null; }
        var people = (await store.GetPeopleAsync(cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        var names = rules.Select(x => people.GetValueOrDefault(x.PersonExternalId, "pessoa removida")).Distinct().Order();
        return $"O filtro é usado por regras de rateio de: {string.Join(", ", names)}. Exclua as regras antes de excluir o filtro.";
    }
}
