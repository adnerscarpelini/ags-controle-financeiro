using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class InvoiceAdjustmentConfiguration : IEntityTypeConfiguration<InvoiceAdjustment>
{
    public void Configure(EntityTypeBuilder<InvoiceAdjustment> builder)
    {
        builder.ToTable("invoice_adjustments");
        builder.HasIndex(x => x.ConfirmationId).IsUnique();
        builder.HasIndex(x => new { x.CardExternalId, x.Month });
    }
}
