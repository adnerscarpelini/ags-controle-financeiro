using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class RecurringTransferConfiguration : IEntityTypeConfiguration<RecurringTransfer>
{
    public void Configure(EntityTypeBuilder<RecurringTransfer> builder)
    {
        builder.ToTable("recurring_transfers");
        builder.Ignore(x => x.StartMonth);
    }
}

internal sealed class TransferOccurrenceConfiguration : IEntityTypeConfiguration<TransferOccurrence>
{
    public void Configure(EntityTypeBuilder<TransferOccurrence> builder)
    {
        builder.ToTable("transfer_occurrences");
        builder.Ignore(x => x.IsConfirmed);
        builder.HasOne<RecurringTransfer>().WithMany().HasForeignKey(x => x.RecurringTransferExternalId).HasPrincipalKey(x => x.ExternalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RecurringTransferExternalId, x.OccurrenceMonth }).IsUnique();
    }
}
