using Ags.Finance.Modules.Invoices.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed partial class InvoiceStore
{
    public async Task<IReadOnlyList<InstallmentAnticipation>> GetAnticipationsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<InstallmentAnticipation>().AsNoTracking().ToListAsync(cancellationToken);
    }
    public async Task AddAnticipationAsync(InstallmentAnticipation anticipation, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(anticipation);
        await context.SaveChangesAsync(cancellationToken);
    }
}
