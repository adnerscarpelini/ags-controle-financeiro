using System.Text;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Transactions.Contracts;
using Ags.Finance.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Transactions.Tests;

public sealed partial class EntryTests
{
    private IEntryAttachmentCatalog Documents => Provider.GetRequiredService<IEntryAttachmentCatalog>();
    private async Task<AttachmentResult> AttachAsync(Guid entryId)
    {
        using var content = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"));
        return await Documents.UploadAsync(entryId, AttachmentKind.Receipt, "fictitious.pdf", content);
    }
    [Fact]
    public async Task TwelveInstallmentsShareOneFileUntilTheLastInstallmentIsDeleted()
    {
        var result = await Entries.CreateInstallmentsAsync(Request() with { CardExternalId = await CreateCardAsync() }, 12);
        var rows = (await Entries.GetAsync()).OrderBy(x => x.InstallmentNumber).ToList();
        Assert.True((await AttachAsync(rows[0].ExternalId)).Succeeded);
        foreach (var row in rows) { Assert.Equal(new(AttachmentOwnerType.Purchase, result.ExternalId!.Value), await Documents.FindOwnerAsync(row.ExternalId)); }
        Assert.Single(Directory.GetFiles(_attachmentsDirectory, "*", SearchOption.AllDirectories));
        await Entries.DeleteAsync(rows[0].ExternalId, EntryChangeScope.OnlyThis);
        Assert.Null(Assert.Single(await Documents.ListAsync()).DeletedAt);
        await Entries.DeleteAsync(rows[1].ExternalId, EntryChangeScope.ThisAndFollowing);
        Assert.NotNull(Assert.Single(await Documents.ListAsync()).DeletedAt);
        Assert.False((await Documents.RestoreAsync(result.ExternalId!.Value, Assert.Single(await Documents.ListAsync()).ExternalId)).Succeeded);
    }
    [Theory]
    [InlineData(RecurringChangeScope.OnlyThis)]
    [InlineData(RecurringChangeScope.ThisAndFollowing)]
    [InlineData(RecurringChangeScope.All)]
    public async Task RecurringEditsPreserveOccurrenceAttachmentsAndDoNotShareThemWithDecember(RecurringChangeScope scope)
    {
        await Entries.CreateRecurringAsync(new(Request() with { Date = new DateOnly(2026, 10, 10) }));
        var november = await OccurrenceAsync(2026, 11);
        var december = await OccurrenceAsync(2026, 12);
        Assert.True((await AttachAsync(november.ExternalId)).Succeeded);
        var original = Assert.Single(await Documents.ListAsync());
        Assert.True((await Entries.EditRecurringAsync(november.ExternalId, new(Change(november) with { Amount = new Money(80) }, scope, 20))).Succeeded);
        Assert.Equal(original.Owner, await Documents.FindOwnerAsync(november.ExternalId));
        Assert.NotEqual(original.Owner, await Documents.FindOwnerAsync(december.ExternalId));
        Assert.Equal(original.StorageKey, Assert.Single(await Documents.ListAsync()).StorageKey);
    }
    [Fact]
    public async Task DuplicatingDoesNotCopyDocumentsAndDeletingTrashesThem()
    {
        var entry = (await Entries.CreateAsync(Request())).ExternalId!.Value;
        Assert.True((await AttachAsync(entry)).Succeeded);
        var duplicate = await Entries.DuplicateAsync(entry);
        Assert.NotEqual(await Documents.FindOwnerAsync(entry), await Documents.FindOwnerAsync(duplicate.ExternalId!.Value));
        Assert.Single(await Documents.ListAsync());
        await Entries.DeleteAsync(entry, EntryChangeScope.OnlyThis);
        Assert.NotNull(Assert.Single(await Documents.ListAsync()).DeletedAt);
    }
    [Fact]
    public async Task FailedUploadDoesNotUndoTheEntryAndCanBeRetried()
    {
        var entry = (await Entries.CreateAsync(Request())).ExternalId!.Value;
        Directory.Delete(_attachmentsDirectory);
        Assert.False((await AttachAsync(entry)).Succeeded);
        Assert.Single(await Entries.GetAsync());
        Directory.CreateDirectory(_attachmentsDirectory);
        Assert.True((await AttachAsync(entry)).Succeeded);
        Assert.Single(await Entries.GetAsync());
    }
}
