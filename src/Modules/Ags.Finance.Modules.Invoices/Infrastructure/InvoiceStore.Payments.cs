using Ags.Finance.Modules.Invoices.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed partial class InvoiceStore
{
    public async Task<IReadOnlyList<InvoicePayment>> GetPaymentsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<InvoicePayment>().AsNoTracking().ToListAsync(cancellationToken);
    }
    public async Task SavePaymentAsync(InvoicePayment payment, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Entry(payment).State = payment.Id == 0 ? EntityState.Added : EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
    }
    public async Task DeletePaymentAsync(InvoicePayment payment, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Remove(payment);
        await context.SaveChangesAsync(cancellationToken);
    }
}
