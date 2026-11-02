using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Invoices.Infrastructure;

internal sealed class InvoicesDbContext : DbContext
{
    public const string TablePrefix = "invoices_";
    public const string MigrationsHistoryTable = "__ef_migrations_invoices";

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
        PersistenceConventions.IgnoreEntityBase(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoicesDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder, TablePrefix);
    }
}
