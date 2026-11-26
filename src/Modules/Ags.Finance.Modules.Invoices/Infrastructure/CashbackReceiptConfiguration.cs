using Ags.Finance.Modules.Invoices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed class CashbackReceiptConfiguration : IEntityTypeConfiguration<CashbackReceipt>
{
    public void Configure(EntityTypeBuilder<CashbackReceipt> builder)
    {
        builder.ToTable("cashback_receipts");
        builder.HasIndex(x => x.ConfirmationId).IsUnique();
        builder.HasIndex(x => x.InvoiceExternalId).IsUnique();
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.TargetInvoiceExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
    }
}
