using Ags.Finance.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Identity.Infrastructure;

internal sealed class IdentityDbContext : DbContext
{
    public const string Schema = "identity";
    public const string MigrationsHistoryTable = "__ef_migrations";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        PersistenceConventions.ConfigureValueConversions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        PersistenceConventions.IgnoreEntityBase(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder);
    }
}
