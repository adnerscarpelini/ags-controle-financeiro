using Ags.Finance.Modules.Invoices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("invoice_payments");
        builder.Property(x => x.Details).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.ConfirmationId).IsUnique();
        builder.HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
    }
}
