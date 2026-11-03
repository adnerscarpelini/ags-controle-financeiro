using Ags.Finance.Modules.Transactions.Domain;
using Ags.Finance.Modules.Transactions.Infrastructure;
using Ags.Finance.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    [Fact]
    public async Task RenamingTheReferenceMonthPreservesStoredYearMonthAndSeriesOffset()
    {
        Assert.True((await Entries.CreateRecurringAsync(new(Request() with { IsPaid = false }))).Succeeded);
        await using var scope = Provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        var series = await context.Set<RecurringEntry>().SingleAsync();
        context.Entry(series).Property(x => x.ReferenceMonthOffset).CurrentValue = 2;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261110010000_RecurringReimbursement");
        await migrator.MigrateAsync();
        Assert.Equal(new ReferenceMonth(2026, 10), (await context.Set<TransactionEntry>().SingleAsync()).Month);
        Assert.Equal(2, (await context.Set<RecurringEntry>().SingleAsync()).ReferenceMonthOffset);
    }
}
