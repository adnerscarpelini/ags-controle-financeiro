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
