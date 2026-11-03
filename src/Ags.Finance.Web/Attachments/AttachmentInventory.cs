using Ags.Finance.Modules.Attachments.Contracts;

namespace Ags.Finance.Web.Attachments;

internal sealed record AttachmentInventoryItem(string StorageKey, string Status);

internal sealed class AttachmentInventory(IAttachmentCatalog catalog, AttachmentStorageOptions options)
{
    public async Task<IReadOnlyList<AttachmentInventoryItem>> VerifyAsync(bool cleanOrphans, CancellationToken cancellationToken)
    {
        if (options.Error is { } error) { throw new IOException(error); }
        if (!Directory.Exists(options.Directory)) { throw new DirectoryNotFoundException("A pasta de anexos está ausente ou não foi montada."); }
        await using var lease = await catalog.AcquireMaintenanceLeaseAsync(cancellationToken);
        var rows = await catalog.ListAsync(cancellationToken);
        var referenced = rows.Select(x => x.StorageKey).ToHashSet(StringComparer.Ordinal);
        var problems = new List<AttachmentInventoryItem>();
        foreach (var row in rows)
        {
            var path = AttachmentBackup.Resolve(options.Directory, row.StorageKey);
            if (!File.Exists(path)) { problems.Add(new(row.StorageKey, "ausente")); }
            else if (!await AttachmentBackup.MatchesAsync(path, new(row.StorageKey, row.SizeBytes, row.Sha256), cancellationToken))
            { problems.Add(new(row.StorageKey, "corrompido")); }
        }
        foreach (var path in Files())
        {
            var key = Path.GetRelativePath(options.Directory, path).Replace('\\', '/');
            if (referenced.Contains(key)) { continue; }
            problems.Add(new(key, cleanOrphans ? "sem registro (removido)" : "sem registro"));
            if (cleanOrphans) { File.Delete(AttachmentBackup.Resolve(options.Directory, key)); }
        }
        return problems;
    }

    public long DiskUsageBytes()
    {
        if (options.Error is { } error) { throw new IOException(error); }
        return Directory.Exists(options.Directory) ? Files().Sum(x => new FileInfo(x).Length) : 0;
    }

    private IEnumerable<string> Files()
        => Directory.EnumerateFiles(options.Directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint })
            .Where(path => !Path.GetRelativePath(options.Directory, path).Split(Path.DirectorySeparatorChar).Any(x => x.StartsWith('.')));
}
