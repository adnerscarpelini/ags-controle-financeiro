using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ags.Finance.Modules.Invoices.Infrastructure.Migrations;

[DbContext(typeof(InvoicesDbContext))]
internal sealed class InvoicesDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("invoices");
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 63);
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        modelBuilder.Entity("Ags.Finance.Modules.Invoices.Domain.Invoice", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("CardExternalId").HasColumnType("uuid");
            b.Property<int>("Month").HasColumnType("integer");
            b.Property<int>("Status").HasColumnType("integer");
            b.Property<bool>("AutoCloseSuppressed").HasColumnType("boolean");
            b.Property<bool>("HasConfirmedPayment").HasColumnType("boolean");
            b.Property<DateTimeOffset?>("ClosedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset?>("PaidAt").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasAlternateKey("ExternalId");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("CardExternalId", "Month").IsUnique();
            b.ToTable("invoices", "invoices");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Invoices.Domain.InvoicePayment", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("ConfirmationId").HasColumnType("uuid");
            b.Property<Guid>("InvoiceExternalId").HasColumnType("uuid");
            b.Property<DateOnly>("Date").HasColumnType("date");
            b.Property<long>("Amount").HasColumnType("bigint");
            b.Property<int>("Kind").HasColumnType("integer");
            b.Property<Guid?>("AccountExternalId").HasColumnType("uuid");
            b.Property<string>("Details").IsRequired().HasMaxLength(1000).HasColumnType("character varying(1000)");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("ConfirmationId").IsUnique();
            b.HasIndex("InvoiceExternalId");
            b.ToTable("invoice_payments", "invoices");
        });
        modelBuilder.Entity("Ags.Finance.Modules.Invoices.Domain.InvoicePayment", b =>
        {
            b.HasOne("Ags.Finance.Modules.Invoices.Domain.Invoice", null).WithMany().HasForeignKey("InvoiceExternalId").HasPrincipalKey("ExternalId").OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
        modelBuilder.Entity("Ags.Finance.Modules.Invoices.Domain.InstallmentAnticipation", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("bigint").HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            b.Property<Guid>("ExternalId").HasColumnType("uuid");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
            b.Property<Guid>("ConfirmationId").HasColumnType("uuid");
            b.Property<Guid>("EntryExternalId").HasColumnType("uuid");
            b.Property<Guid>("PurchaseExternalId").HasColumnType("uuid");
            b.Property<Guid>("CardExternalId").HasColumnType("uuid");
            b.Property<int>("Month").HasColumnType("integer");
            b.Property<Guid[]>("EntryExternalIds").IsRequired().HasColumnType("uuid[]");
            b.Property<int>("Count").HasColumnType("integer");
            b.Property<long>("Total").HasColumnType("bigint");
            b.Property<long>("Discount").HasColumnType("bigint");
            b.HasKey("Id");
            b.HasIndex("ExternalId").IsUnique();
            b.HasIndex("ConfirmationId").IsUnique();
            b.ToTable("installment_anticipations", "invoices");
        });
    }
}
