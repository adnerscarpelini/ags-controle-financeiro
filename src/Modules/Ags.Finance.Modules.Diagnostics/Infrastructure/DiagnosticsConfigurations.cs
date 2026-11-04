using Ags.Finance.Modules.Diagnostics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Diagnostics.Infrastructure;

internal sealed class ErrorRecordConfiguration : IEntityTypeConfiguration<ErrorRecord>
{
    public void Configure(EntityTypeBuilder<ErrorRecord> builder)
    {
        builder.ToTable("error_records");
        builder.Property(x => x.Fingerprint).HasMaxLength(ErrorRecord.FingerprintLength).IsRequired();
        builder.Property(x => x.ExceptionType).HasMaxLength(ErrorRecord.MaxExceptionTypeLength).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(ErrorRecord.MaxSourceLength).IsRequired();
        builder.Property(x => x.StackTrace).IsRequired();
        builder.Property(x => x.SqlState).HasMaxLength(ErrorRecord.MaxSqlStateLength);
        builder.HasIndex(x => x.Fingerprint).IsUnique();
        builder.HasIndex(x => x.LastOccurredAt);
    }
}
