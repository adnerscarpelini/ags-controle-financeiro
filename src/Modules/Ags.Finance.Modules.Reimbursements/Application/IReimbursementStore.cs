using Ags.Finance.Modules.Reimbursements.Domain;

namespace Ags.Finance.Modules.Reimbursements.Application;

internal interface IReimbursementStore
{
    Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken cancellationToken);
    Task<bool> SavePersonAsync(Person person, CancellationToken cancellationToken);
    Task DeletePersonAsync(Person person, CancellationToken cancellationToken);
    Task<IReadOnlyList<SharingRule>> GetRulesAsync(CancellationToken cancellationToken);
    Task<bool> SaveRuleAsync(SharingRule rule, CancellationToken cancellationToken);
    Task DeleteRuleAsync(SharingRule rule, CancellationToken cancellationToken);
}
