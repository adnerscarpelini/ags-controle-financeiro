using Microsoft.EntityFrameworkCore;

namespace Ags.Finance.Modules.Diagnostics.Infrastructure;

internal sealed class DiagnosticsDbContext : DbContext
{
    public const string Schema = "diagnostics";
    public const string MigrationsHistoryTable = "__ef_migrations";

    public DiagnosticsDbContext(DbContextOptions<DiagnosticsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        PersistenceConventions.IgnoreEntityBase(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiagnosticsDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder);
    }
}
