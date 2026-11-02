using Ags.Finance.Modules.Accounts.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Accounts.Infrastructure;

internal sealed class CardLastDigitsConfiguration : IEntityTypeConfiguration<CardLastDigits>
{
    public void Configure(EntityTypeBuilder<CardLastDigits> builder)
    {
        builder.ToTable("card_last_digits");
        builder.Property(x => x.Digits).HasMaxLength(4).IsRequired();
        builder.Property(x => x.Label).IsRequired();
        builder.HasIndex(x => new { x.CardExternalId, x.Digits }).IsUnique();
    }
}
