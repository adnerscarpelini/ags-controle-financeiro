using Ags.Finance.Modules.Attachments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Attachments.Infrastructure;

internal sealed class AttachmentsDbContext(DbContextOptions<AttachmentsDbContext> options) : DbContext(options)
{
    public const string Schema = "attachments";
    public const string MigrationsHistoryTable = "__ef_migrations";
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        PersistenceConventions.IgnoreEntityBase(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AttachmentsDbContext).Assembly);
        PersistenceConventions.ApplyEntityRules(modelBuilder);
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("documents");
        builder.Property(x => x.OwnerType).HasMaxLength(30);
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255);
        builder.Property(x => x.ContentType).HasMaxLength(50);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.Property(x => x.StorageKey).HasMaxLength(200);
        builder.HasIndex(x => new { x.OwnerType, x.OwnerExternalId, x.Sha256 }).IsUnique();
    }
}
