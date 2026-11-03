using System.Security.Cryptography;
using System.Text;
using Ags.Finance.Modules.Attachments.Application;
using Ags.Finance.Modules.Attachments.Contracts;
using Ags.Finance.Modules.Attachments.Infrastructure;
using Ags.Finance.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Ags.Finance.Modules.Attachments.Tests;

public sealed class AttachmentCatalogTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ags-attachments-" + Guid.NewGuid().ToString("N"));
    private readonly AttachmentOwner _owner = new(AttachmentOwnerType.Entry, Guid.CreateVersion7());
    private PostgresTestDatabase? _database;
    private ServiceProvider? _provider;
    private IAttachmentCatalog Catalog => (_provider ?? throw new InvalidOperationException()).GetRequiredService<IAttachmentCatalog>();
    private PostgresTestDatabase Database => _database ?? throw new InvalidOperationException();
    private static byte[] Pdf => Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj<</Type /Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n");

    public async Task InitializeAsync()
    {
        _database = await PostgresTestDatabase.CreateAsync();
        Directory.CreateDirectory(_directory);
        _provider = new ServiceCollection().AddAttachmentsModule(Database.ConnectionString, new(_directory)).BuildServiceProvider();
        await _provider.MigrateAttachmentsModuleAsync();
    }
    public async Task DisposeAsync()
    {
        if (_provider is not null) { await _provider.DisposeAsync(); }
        if (_database is not null) { await _database.DisposeAsync(); }
        if (Directory.Exists(_directory)) { Directory.Delete(_directory, true); }
    }
    [Fact]
    public async Task UploadPreservesOriginalBytesAndGeneratesItsOwnImmutablePath()
    {
        using var source = new MemoryStream(Pdf);
        var result = await Catalog.UploadAsync(_owner, AttachmentKind.Receipt, "../evidence.exe", source);
        Assert.True(result.Succeeded, result.Error);
        var row = Assert.Single(await Catalog.ListAsync());
        Assert.Equal("evidence.exe", row.OriginalFileName);
        Assert.Equal("application/pdf", row.ContentType);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Pdf)), row.Sha256);
        Assert.StartsWith("entries/", row.StorageKey, StringComparison.Ordinal);
        Assert.EndsWith($"_receipt_{row.ExternalId}.pdf", row.StorageKey, StringComparison.Ordinal);
        Assert.Equal(Pdf, await File.ReadAllBytesAsync(Path.Combine(_directory, row.StorageKey)));
        Assert.Equal(7, row.ExternalId.Version);
    }
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task ImagesAreIdentifiedByTheirContentAndPreservedWithoutChanges(SKEncodedImageFormat format, string contentType)
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Green);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        var bytes = data.ToArray();
        using var source = new MemoryStream(bytes);
        Assert.True((await Catalog.UploadAsync(_owner, AttachmentKind.Other, "misleading.pdf", source)).Succeeded);
        var row = Assert.Single(await Catalog.ListAsync());
        Assert.Equal(contentType, row.ContentType);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(_directory, row.StorageKey)));
    }
    [Fact]
    public async Task DeniedDirectoryReturnsAnErrorAndTheRestOfTheCatalogContinuesWorking()
    {
        var provider = _provider ?? throw new InvalidOperationException();
        var denied = new AttachmentCatalog(provider.GetRequiredService<IAttachmentStore>(), new DeniedFiles(), new(_directory), TimeProvider.System);
        using var source = new MemoryStream(Pdf);
        Assert.False((await denied.UploadAsync(_owner, AttachmentKind.Other, "fictitious.pdf", source)).Succeeded);
        Assert.Empty(await Catalog.ListAsync());
        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }
    [Theory]
    [InlineData("MZ this is an executable renamed to PDF")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg'></svg>")]
    [InlineData("%PDF-1.7 truncated")]
    public async Task InvalidContentLeavesNoFileOrMetadata(string text)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(text));
        Assert.False((await Catalog.UploadAsync(_owner, AttachmentKind.Other, "fake.pdf", source)).Succeeded);
        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
        Assert.Empty(await Catalog.ListAsync());
    }
    [Fact]
    public async Task OverLimitUploadIsRemoved()
    {
        using var source = new MemoryStream(new byte[20 * 1024 * 1024 + 1]);
        Assert.False((await Catalog.UploadAsync(_owner, AttachmentKind.Other, "large.pdf", source)).Succeeded);
        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task DuplicateIsRejectedAndOnlyOneFileRemains()
    {
        using var first = new MemoryStream(Pdf);
        using var second = new MemoryStream(Pdf);
        Assert.True((await Catalog.UploadAsync(_owner, AttachmentKind.Receipt, "first.pdf", first)).Succeeded);
        Assert.False((await Catalog.UploadAsync(_owner, AttachmentKind.Other, "second.pdf", second)).Succeeded);
        Assert.Single(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
        Assert.Single(await Catalog.ListAsync());
    }
    [Fact]
    public async Task DatabaseFailureRollsBackTheDefinitiveFile()
    {
        await Database.ExecuteAsync("DROP TABLE attachments.documents");
        using var source = new MemoryStream(Pdf);
        Assert.False((await Catalog.UploadAsync(_owner, AttachmentKind.Receipt, "fake.pdf", source)).Succeeded);
        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task MissingDirectoryReturnsAnErrorWithoutCreatingIt()
    {
        Directory.Delete(_directory);
        using var source = new MemoryStream(Pdf);
        Assert.False((await Catalog.UploadAsync(_owner, AttachmentKind.Receipt, "fake.pdf", source)).Succeeded);
        Assert.False(Directory.Exists(_directory));
    }
    [Fact]
    public async Task MissingMountDoesNotPurgeMetadataWhileItsFilesAreUnavailable()
    {
        using var source = new MemoryStream(Pdf);
        Assert.True((await Catalog.UploadAsync(_owner, AttachmentKind.Receipt, "fictitious.pdf", source)).Succeeded);
        await Database.ExecuteAsync("UPDATE attachments.documents SET \"DeletedAt\" = now() - interval '31 days'");
        var provider = _provider ?? throw new InvalidOperationException();
        var unavailableOptions = new AttachmentStorageOptions(_directory + "-unmounted");
        var unavailable = new AttachmentCatalog(provider.GetRequiredService<IAttachmentStore>(), new AttachmentFiles(unavailableOptions), unavailableOptions, TimeProvider.System);
        await unavailable.PurgeAsync();
        Assert.Single(await Catalog.ListAsync());
        Assert.Single(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task TrashRejectsAnotherOwnerAndAllowsDownloadAndRestoreWithinThirtyDays()
    {
        using var source = new MemoryStream(Pdf);
        var id = (await Catalog.UploadAsync(_owner, AttachmentKind.Bill, "fake.pdf", source)).ExternalId!.Value;
        Assert.False((await Catalog.TrashAsync(new(AttachmentOwnerType.Entry, Guid.NewGuid()), id)).Succeeded);
        Assert.True((await Catalog.TrashAsync(_owner, id)).Succeeded);
        var file = await Catalog.OpenAsync(id);
        Assert.NotNull(file);
        await file.Stream.DisposeAsync();
        Assert.True((await Catalog.RestoreAsync(_owner, id)).Succeeded);
        Assert.Null(Assert.Single(await Catalog.ListAsync()).DeletedAt);
    }
    [Theory]
    [InlineData(29, false)]
    [InlineData(31, true)]
    public async Task PurgeHonorsTheThirtyDayDeadline(int days, bool removed)
    {
        using var source = new MemoryStream(Pdf);
        var id = (await Catalog.UploadAsync(_owner, AttachmentKind.Bill, "fake.pdf", source)).ExternalId!.Value;
        await Database.ExecuteAsync($"UPDATE attachments.documents SET \"DeletedAt\" = now() - interval '{days} days'");
        await Catalog.PurgeAsync();
        Assert.Equal(removed, (await Catalog.ListAsync()).Count == 0);
        Assert.Equal(removed, Directory.GetFiles(_directory, "*", SearchOption.AllDirectories).Length == 0);
        if (removed) { Assert.Null(await Catalog.OpenAsync(id)); }
    }
    private sealed class DeniedFiles : IAttachmentFiles
    {
        public bool IsDirectoryAvailable => false;
        public Task<(long Bytes, string Sha256)> WriteAsync(string temporaryKey, Stream source, CancellationToken cancellationToken) => throw new UnauthorizedAccessException();
        public (string ContentType, string Extension)? Identify(string temporaryKey) => throw new NotSupportedException();
        public void Move(string temporaryKey, string storageKey) => throw new NotSupportedException();
        public void Delete(string storageKey) { }
        public Stream Open(string storageKey) => throw new NotSupportedException();
    }
}
