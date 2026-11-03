using Ags.Finance.Modules.Invoices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed class InstallmentAnticipationConfiguration : IEntityTypeConfiguration<InstallmentAnticipation>
{
    public void Configure(EntityTypeBuilder<InstallmentAnticipation> builder)
    {
        builder.ToTable("installment_anticipations");
        builder.HasIndex(x => x.ConfirmationId).IsUnique();
    }
}
