using Ags.Finance.Modules.Reimbursements.Application;
using Ags.Finance.Modules.Reimbursements.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure;

internal sealed class ReimbursementStore(IDbContextFactory<ReimbursementsDbContext> factory) : IReimbursementStore
{
    public async Task<IReadOnlyList<Person>> GetPeopleAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Person>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> SavePersonAsync(Person person, CancellationToken cancellationToken) => SaveAsync(person, cancellationToken);

    public Task DeletePersonAsync(Person person, CancellationToken cancellationToken) => DeleteAsync(person, cancellationToken);

    public async Task<IReadOnlyList<SharingRule>> GetRulesAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<SharingRule>().AsNoTracking().OrderBy(x => x.StartMonth).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<bool> SaveRuleAsync(SharingRule rule, CancellationToken cancellationToken) => SaveAsync(rule, cancellationToken);

    public Task DeleteRuleAsync(SharingRule rule, CancellationToken cancellationToken) => DeleteAsync(rule, cancellationToken);

    private async Task<bool> SaveAsync<T>(T entity, CancellationToken cancellationToken)
        where T : Entity
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(entity).State = entity.Id == 0 ? EntityState.Added : EntityState.Modified;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    private async Task DeleteAsync<T>(T entity, CancellationToken cancellationToken)
        where T : Entity
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(entity).State = EntityState.Deleted;
        await context.SaveChangesAsync(cancellationToken);
    }
}
