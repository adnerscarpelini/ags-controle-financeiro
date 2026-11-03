using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;

namespace Ags.Finance.Modules.Reimbursements.Application.ManageSharingRules;

internal sealed class SharingRuleCatalog(IReimbursementStore store, ISavedFilterCatalog filters) : ISharingRuleCatalog
{
    public async Task<IReadOnlyList<SharingRuleDetails>> GetAsync(Guid? personExternalId = null, CancellationToken cancellationToken = default)
    {
        var names = (await filters.GetAsync(cancellationToken)).ToDictionary(x => x.ExternalId, x => x.Name);
        return (await store.GetRulesAsync(cancellationToken)).Where(x => personExternalId is null || x.PersonExternalId == personExternalId)
            .Select(x => new SharingRuleDetails(x.ExternalId, x.PersonExternalId, x.SavedFilterExternalId, names.GetValueOrDefault(x.SavedFilterExternalId, "Filtro removido"), x.Percent, x.StartMonth)).ToList();
    }

    public async Task<ReimbursementResult> SaveAsync(Guid? externalId, Guid personExternalId, Guid savedFilterExternalId, decimal percent, AccountingMonth startMonth, CancellationToken cancellationToken = default)
    {
        var rules = await store.GetRulesAsync(cancellationToken);
        var rule = externalId is null ? new SharingRule() : rules.FirstOrDefault(x => x.ExternalId == externalId);
        if (rule is null) { return new(null, "Regra não encontrada."); }
        if (!(await store.GetPeopleAsync(cancellationToken)).Any(x => x.ExternalId == personExternalId)) { return new(null, "Selecione uma pessoa cadastrada."); }
        if (await filters.FindAsync(savedFilterExternalId, cancellationToken) is null) { return new(null, "Selecione um filtro salvo existente."); }
        var error = rule.Update(personExternalId, savedFilterExternalId, percent, startMonth);
        if (error is not null) { return new(null, error); }
        if (rules.Any(x => x.ExternalId != rule.ExternalId && x.PersonExternalId == personExternalId && x.SavedFilterExternalId == savedFilterExternalId && x.StartMonth == startMonth))
        {
            return new(null, "Já existe uma regra desta pessoa com o mesmo filtro e mês inicial.");
        }
        return await store.SaveRuleAsync(rule, cancellationToken) ? new(rule.ExternalId, null) : new(null, "Já existe uma regra desta pessoa com o mesmo filtro e mês inicial.");
    }

    public async Task<ReimbursementResult> DeleteAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var rule = (await store.GetRulesAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (rule is null) { return new(null, "Regra não encontrada."); }
        await store.DeleteRuleAsync(rule, cancellationToken);
        return new(externalId, null);
    }
}
