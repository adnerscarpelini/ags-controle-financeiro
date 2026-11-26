using Ags.Finance.Modules.Invoices.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed partial class InvoiceStore
{
    public async Task<IReadOnlyList<CashbackReceipt>> GetCashbackReceiptsAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Set<CashbackReceipt>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task AddCashbackReceiptAsync(CashbackReceipt receipt, InvoicePayment? allowance, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        context.Add(receipt);
        if (allowance is not null) { context.Add(allowance); }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCashbackReceiptAsync(CashbackReceipt receipt, InvoicePayment? allowance, CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        if (allowance is not null) { context.Remove(allowance); }
        context.Remove(receipt);
        await context.SaveChangesAsync(cancellationToken);
    }
}
