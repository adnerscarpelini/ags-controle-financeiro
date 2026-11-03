using Ags.Finance.Modules.Reimbursements.Contracts;
using Ags.Finance.Modules.Reimbursements.Domain;

namespace Ags.Finance.Modules.Reimbursements.Application.ManagePeople;

internal sealed class PersonCatalog(IReimbursementStore store) : IPersonCatalog
{
    public async Task<IReadOnlyList<PersonDetails>> GetAsync(CancellationToken cancellationToken = default)
        => (await store.GetPeopleAsync(cancellationToken)).Select(x => new PersonDetails(x.ExternalId, x.Name, x.PayerDescription)).ToList();

    public async Task<ReimbursementResult> SaveAsync(Guid? externalId, string name, string payerDescription, CancellationToken cancellationToken = default)
    {
        var people = await store.GetPeopleAsync(cancellationToken);
        var person = externalId is null ? new Person() : people.FirstOrDefault(x => x.ExternalId == externalId);
        if (person is null) { return new(null, "Pessoa não encontrada."); }
        var error = person.Update(name, payerDescription);
        if (error is not null) { return new(null, error); }
        var duplicate = people.FirstOrDefault(x => x.ExternalId != person.ExternalId && x.NormalizedName == person.NormalizedName);
        if (duplicate is not null) { return new(null, $"Esta pessoa já existe: {duplicate.Name}."); }
        return await store.SavePersonAsync(person, cancellationToken) ? new(person.ExternalId, null) : new(null, "Esta pessoa já existe.");
    }

    public async Task<ReimbursementResult> DeleteAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        var person = (await store.GetPeopleAsync(cancellationToken)).FirstOrDefault(x => x.ExternalId == externalId);
        if (person is null) { return new(null, "Pessoa não encontrada."); }
        if ((await store.GetRulesAsync(cancellationToken)).Any(x => x.PersonExternalId == externalId))
        {
            return new(null, $"{person.Name} possui regras de rateio. Exclua as regras antes de excluir a pessoa.");
        }
        if ((await store.GetReceiptsAsync(externalId, cancellationToken)).Count > 0)
        {
            return new(null, $"{person.Name} possui reembolsos recebidos e não pode ser excluída.");
        }
        await store.DeletePersonAsync(person, cancellationToken);
        return new(externalId, null);
    }
}
