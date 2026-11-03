using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Attachments.Infrastructure.Migrations;

[DbContext(typeof(AttachmentsDbContext))]
internal sealed class AttachmentsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("attachments");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Attachments.Domain.Attachment", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn).HasColumnType("bigint");
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("OwnerType").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
            b.Property<Guid>("OwnerExternalId").HasColumnType("uuid");
            b.Property<string>("Kind").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<string>("OriginalFileName").IsRequired().HasMaxLength(255).HasColumnType("character varying(255)");
            b.Property<string>("ContentType").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
            b.Property<long>("SizeBytes").HasColumnType("bigint");
            b.Property<string>("Sha256").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("StorageKey").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<DateTimeOffset?>("DeletedAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("OwnerType", "OwnerExternalId", "Sha256").IsUnique();
            b.ToTable("documents", "attachments");
        });
    }
}
