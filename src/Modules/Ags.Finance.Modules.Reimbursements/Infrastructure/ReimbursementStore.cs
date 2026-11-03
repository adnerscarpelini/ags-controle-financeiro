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

    public async Task<IReadOnlyList<ReimbursementReceipt>> GetReceiptsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<ReimbursementReceipt>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReimbursementReceiptItem>> GetReceiptItemsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<ReimbursementReceiptItem>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddReceiptAsync(ReimbursementReceipt receipt, IReadOnlyList<ReimbursementReceiptItem> items, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(receipt);
        context.AddRange(items);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteReceiptAsync(ReimbursementReceipt receipt, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(receipt).State = EntityState.Deleted;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsIncomeOfReceiptAsync(Guid entryExternalId, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<ReimbursementReceipt>().AnyAsync(x => x.IncomeEntryExternalId == entryExternalId, cancellationToken);
    }

    public async Task<bool> IsReceivedExpenseAsync(Guid entryExternalId, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<ReimbursementReceiptItem>().AnyAsync(x => x.ExpenseEntryExternalId == entryExternalId, cancellationToken);
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
