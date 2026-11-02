using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class InstallmentPurchaseConfiguration : IEntityTypeConfiguration<InstallmentPurchase>
{
    public void Configure(EntityTypeBuilder<InstallmentPurchase> builder)
    {
        builder.ToTable("installment_purchases");
    }
}
