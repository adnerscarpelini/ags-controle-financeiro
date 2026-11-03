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

    public async Task<bool> SavePersonAsync(Person person, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(person).State = person.Id == 0 ? EntityState.Added : EntityState.Modified;
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

    public async Task DeletePersonAsync(Person person, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(person).State = EntityState.Deleted;
        await context.SaveChangesAsync(cancellationToken);
    }
}
