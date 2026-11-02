using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class EntryConfiguration : IEntityTypeConfiguration<TransactionEntry>
{
    public void Configure(EntityTypeBuilder<TransactionEntry> builder)
    {
        builder.ToTable("entries");
        builder.Property(x => x.Details).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.Month);
        builder.HasIndex(x => new { x.CardExternalId, x.Month });
        builder.HasIndex(x => new { x.AccountExternalId, x.EffectiveDate });
        builder.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subcategory>().WithMany().HasForeignKey(x => x.SubcategoryExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InstallmentPurchase>().WithMany().HasForeignKey(x => x.InstallmentPurchaseExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.InstallmentPurchaseExternalId, x.InstallmentNumber }).IsUnique();
    }
}
