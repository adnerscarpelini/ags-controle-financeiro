using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Diagnostics.Infrastructure.Migrations;

[DbContext(typeof(DiagnosticsDbContext))]
internal sealed class DiagnosticsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("diagnostics");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Diagnostics.Domain.ErrorRecord", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Fingerprint").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
            b.Property<string>("ExceptionType").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
            b.Property<string>("Source").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
            b.Property<string>("StackTrace").IsRequired().HasColumnType("text");
            b.Property<string>("SqlState").HasMaxLength(5).HasColumnType("character varying(5)");
            b.Property<DateTimeOffset>("FirstOccurredAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("LastOccurredAt").HasColumnType("timestamp with time zone");
            b.Property<long>("Occurrences").HasColumnType("bigint");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("Fingerprint").IsUnique();
            b.HasIndex("LastOccurredAt");
            b.ToTable("error_records", "diagnostics");
        });
    }
}
