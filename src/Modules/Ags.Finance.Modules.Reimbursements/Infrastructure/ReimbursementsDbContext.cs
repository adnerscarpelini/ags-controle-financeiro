using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Reimbursements.Infrastructure;

internal sealed class ReimbursementsDbContext : DbContext
{
    public const string TablePrefix = "reimbursements_";
    public const string MigrationsHistoryTable = "__ef_migrations_reimbursements";

    public ReimbursementsDbContext(DbContextOptions<ReimbursementsDbContext> options)
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReimbursementsDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder, TablePrefix);
    }
}
