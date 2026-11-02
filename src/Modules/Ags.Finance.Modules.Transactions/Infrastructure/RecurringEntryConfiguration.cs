using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class RecurringEntryConfiguration : IEntityTypeConfiguration<RecurringEntry>
{
    public void Configure(EntityTypeBuilder<RecurringEntry> builder)
    {
        builder.ToTable("recurring_entries");
        builder.Property(x => x.Details).HasMaxLength(1000).IsRequired();
        builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subcategory>().WithMany().HasForeignKey(x => x.SubcategoryExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
    }
}
