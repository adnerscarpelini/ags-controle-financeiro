using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class TransactionsDbContext : DbContext
{
    public const string Schema = "transactions";
    public const string MigrationsHistoryTable = "__ef_migrations";

    public TransactionsDbContext(DbContextOptions<TransactionsDbContext> options)
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransactionsDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder);
    }
}
