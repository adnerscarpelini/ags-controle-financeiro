using Ags.Finance.Modules.Transactions.Application;
using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class MerchantStore(IDbContextFactory<TransactionsDbContext> factory) : IMerchantStore
{
    public async Task<IReadOnlyList<Merchant>> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Merchant>().AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<bool> SaveAsync(Merchant merchant, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(merchant).State = merchant.Id == 0 ? EntityState.Added : EntityState.Modified;
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

    public async Task<string?> MergeAsync(Guid sourceExternalId, Guid targetExternalId, CancellationToken cancellationToken)
    {
        try
        {
            return await MergeCoreAsync(sourceExternalId, targetExternalId, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return "Uma descrição foi alterada durante a operação. Consulte novamente antes de mesclar.";
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.ForeignKeyViolation })
        {
            return "Uma descrição foi alterada ou está protegida por outra operação. Consulte novamente antes de mesclar.";
        }
    }

    private async Task<string?> MergeCoreAsync(Guid sourceExternalId, Guid targetExternalId, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = System.Transactions.Transaction.Current is null
            ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
        var source = await context.Set<Merchant>().SingleOrDefaultAsync(x => x.ExternalId == sourceExternalId, cancellationToken);
        var target = await context.Set<Merchant>().SingleOrDefaultAsync(x => x.ExternalId == targetExternalId, cancellationToken);
        if (source is null || target is null || !target.IsActive)
        {
            return "Selecione a descrição de origem e uma descrição ativa para manter.";
        }
        var entries = await context.Set<TransactionEntry>().Where(x => x.MerchantExternalId == sourceExternalId).ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.ReplaceMerchant(targetExternalId);
        }
        context.Remove(source);
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) { await transaction.CommitAsync(cancellationToken); }
        return null;
    }
}
