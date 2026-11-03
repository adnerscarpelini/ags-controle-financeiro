using Ags.Finance.Modules.Reimbursements.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayerDescription).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}

internal sealed class ReimbursementReceiptConfiguration : IEntityTypeConfiguration<ReimbursementReceipt>
{
    public void Configure(EntityTypeBuilder<ReimbursementReceipt> builder)
    {
        builder.ToTable("receipts");
        builder.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.EntryExternalId).IsUnique();
        builder.HasIndex(x => new { x.PersonExternalId, x.Month });
    }
}

internal sealed class SharingRuleConfiguration : IEntityTypeConfiguration<SharingRule>
{
    public void Configure(EntityTypeBuilder<SharingRule> builder)
    {
        builder.ToTable("sharing_rules");
        builder.Property(x => x.Percent).HasPrecision(5, 2);
        builder.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PersonExternalId, x.SavedFilterExternalId, x.StartMonth }).IsUnique();
        builder.HasIndex(x => x.SavedFilterExternalId);
    }
}
