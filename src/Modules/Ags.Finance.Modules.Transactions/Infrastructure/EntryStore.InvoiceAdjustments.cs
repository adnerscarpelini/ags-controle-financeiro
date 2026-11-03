using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed partial class EntryStore
{
    public async Task<IReadOnlyList<InvoiceAdjustment>> GetInvoiceAdjustmentsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<InvoiceAdjustment>().AsNoTracking().ToListAsync(cancellationToken);
    }
    public async Task AddInvoiceAdjustmentAsync(InvoiceAdjustment adjustment, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(adjustment);
        await context.SaveChangesAsync(cancellationToken);
    }
}
