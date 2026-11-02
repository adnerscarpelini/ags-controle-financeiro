using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Accounts.Infrastructure;

internal sealed class AccountsDbContext : DbContext
{
    public const string Schema = "accounts";
    public const string MigrationsHistoryTable = "__ef_migrations";

    public AccountsDbContext(DbContextOptions<AccountsDbContext> options)
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountsDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder);
    }
}
