using Ags.Finance.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Identity.Infrastructure;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(user => user.UserName).IsRequired();
        builder.Property(user => user.PasswordHash).IsRequired();
        builder.Property(user => user.SecurityStamp).IsRequired();
    }
}
