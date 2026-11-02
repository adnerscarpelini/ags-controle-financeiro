using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed class InvoicesDbContext : DbContext
{
    public const string Schema = "invoices";
    public const string MigrationsHistoryTable = "__ef_migrations";

    public InvoicesDbContext(DbContextOptions<InvoicesDbContext> options)
        : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        PersistenceConventions.ConfigureValueConversions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        PersistenceConventions.IgnoreEntityBase(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoicesDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder);
    }
}
