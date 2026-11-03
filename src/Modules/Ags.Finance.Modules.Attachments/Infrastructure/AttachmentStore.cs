using System.Transactions;
using Ags.Finance.Modules.Attachments.Application;
using Ags.Finance.Modules.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ags.Finance.Modules.Attachments.Infrastructure;

internal sealed class AttachmentStore(IDbContextFactory<AttachmentsDbContext> factory, NpgsqlDataSource source) : IAttachmentStore
{
    public async Task<IReadOnlyList<Attachment>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Set<Attachment>().AsNoTracking().ToListAsync(cancellationToken);
    }
    public async Task<bool> SaveAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (attachment.Id == 0) { db.Add(attachment); } else { db.Update(attachment); }
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
        catch (Exception exception) when (exception is DbUpdateException or NpgsqlException) { throw new IOException("Metadata persistence failed.", exception); }
    }
    public async Task DeleteAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<IAsyncDisposable> AcquireMaintenanceLeaseAsync(CancellationToken cancellationToken)
    {
        using var suppression = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        var connection = await source.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(73477630)", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
            suppression.Complete();
            return new MaintenanceLease(connection);
        }
        catch { await connection.DisposeAsync(); throw; }
    }
    private sealed class MaintenanceLease(NpgsqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(73477630)", connection);
                await command.ExecuteNonQueryAsync();
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
