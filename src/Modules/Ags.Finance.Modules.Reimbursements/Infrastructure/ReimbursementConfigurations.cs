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
        builder.Property(x => x.DefaultPercent).HasPrecision(5, 2);
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}

internal sealed class ReimbursementReceiptConfiguration : IEntityTypeConfiguration<ReimbursementReceipt>
{
    public void Configure(EntityTypeBuilder<ReimbursementReceipt> builder)
    {
        builder.ToTable("reimbursement_receipts");
        builder.HasOne<Person>().WithMany().HasForeignKey(x => x.PersonExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.IncomeEntryExternalId).IsUnique();
    }
}

internal sealed class ReimbursementReceiptItemConfiguration : IEntityTypeConfiguration<ReimbursementReceiptItem>
{
    public void Configure(EntityTypeBuilder<ReimbursementReceiptItem> builder)
    {
        builder.ToTable("reimbursement_receipt_items");
        builder.Property(x => x.Percent).HasPrecision(5, 2);
        builder.HasOne<ReimbursementReceipt>().WithMany().HasForeignKey(x => x.ReceiptExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ExpenseEntryExternalId).IsUnique();
    }
}
