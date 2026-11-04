using Ags.Finance.Modules.Accounts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Accounts.Infrastructure;

internal sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("banks");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new
        {
            x.BankExternalId,
            x.NormalizedName
        }).IsUnique();
        builder.Property(x => x.Color).HasMaxLength(20).IsRequired();
    }
}

internal sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("credit_cards");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(20).IsRequired();
    }
}
