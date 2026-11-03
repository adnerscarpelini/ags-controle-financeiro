using Ags.Finance.Modules.Transactions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ags.Finance.Modules.Transactions.Infrastructure;

internal sealed class EntryRepetitionConfiguration : IEntityTypeConfiguration<EntryRepetition>
{
    public void Configure(EntityTypeBuilder<EntryRepetition> builder) => builder.ToTable("entry_repetitions");
}
