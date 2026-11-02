using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Identity.Infrastructure;

internal sealed class IdentityDbContext : DbContext
{
    public const string TablePrefix = "identity_";
    public const string MigrationsHistoryTable = "__ef_migrations_identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        PersistenceConventions.ConfigureValueConversions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PersistenceConventions.IgnoreEntityBase(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder, TablePrefix);
    }
}
