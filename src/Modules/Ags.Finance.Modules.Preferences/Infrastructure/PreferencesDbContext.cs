using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Preferences.Infrastructure;

internal sealed class PreferencesDbContext : DbContext
{
    public const string Schema = "preferences";
    public const string MigrationsHistoryTable = "__ef_migrations";

    public PreferencesDbContext(DbContextOptions<PreferencesDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        PersistenceConventions.IgnoreEntityBase(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PreferencesDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder);
    }
}
