using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Preferences.Infrastructure.Migrations;

[DbContext(typeof(PreferencesDbContext))]
internal sealed class PreferencesDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("preferences");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Preferences.Domain.UserPreferences", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<string>("Theme").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<string>("StartScreen").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<string>("RecurringMode").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
            b.Property<int>("PendingWindowDays").HasColumnType("integer");
            b.Property<int>("TopCategories").HasColumnType("integer");
            b.Property<bool>("ShowSummary").HasColumnType("boolean");
            b.Property<bool>("ShowPending").HasColumnType("boolean");
            b.Property<bool>("ShowCategories").HasColumnType("boolean");
            b.Property<bool>("ShowInvoices").HasColumnType("boolean");
            b.Property<int>("PageSize").HasColumnType("integer");
            b.Property<int>("SessionHours").HasColumnType("integer");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.ToTable("user_preferences", "preferences");
        });
    }
}
