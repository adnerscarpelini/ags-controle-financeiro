using Ags.Finance.Modules.Preferences.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Preferences.Infrastructure;

internal sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("user_preferences");
        builder.Property(x => x.Theme).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.StartScreen).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RecurringMode).HasConversion<string>().HasMaxLength(20).IsRequired();
    }
}
