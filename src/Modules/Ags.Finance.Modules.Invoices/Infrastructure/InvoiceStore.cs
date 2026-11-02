using Ags.Finance.Modules.Invoices.Application;
using Ags.Finance.Modules.Invoices.Domain;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed class InvoiceStore(IDbContextFactory<InvoicesDbContext> factory) : IInvoiceStore
{
    public async Task<IReadOnlyList<Invoice>> GetAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Invoice>().AsNoTracking().ToListAsync(cancellationToken);
    }
    public async Task<Invoice?> FindAsync(Guid cardExternalId, AccountingMonth month, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<Invoice>().AsNoTracking().SingleOrDefaultAsync(x => x.CardExternalId == cardExternalId && x.Month == month, cancellationToken);
    }
    public async Task SaveAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(invoice).State = invoice.Id == 0 ? EntityState.Added : EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
    }
}
