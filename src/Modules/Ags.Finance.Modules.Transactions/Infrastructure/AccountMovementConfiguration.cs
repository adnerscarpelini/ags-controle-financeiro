using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class AccountMovementConfiguration : IEntityTypeConfiguration<AccountMovement>
{
    public void Configure(EntityTypeBuilder<AccountMovement> builder)
    {
        builder.ToTable("account_movements");
        builder.HasIndex(x => new { x.OperationExternalId, x.AccountExternalId }).IsUnique();
        builder.HasIndex(x => new { x.AccountExternalId, x.Date });
    }
}
