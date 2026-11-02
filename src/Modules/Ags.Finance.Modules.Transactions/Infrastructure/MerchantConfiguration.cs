using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("merchants");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
        builder.HasOne<Subcategory>().WithMany().HasForeignKey(x => x.DefaultSubcategoryExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
    }
}
