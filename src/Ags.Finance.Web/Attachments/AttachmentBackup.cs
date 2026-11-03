using System.Security.Cryptography;
using System.Text.Json;
using Ags.Finance.Modules.Attachments.Contracts;
using Npgsql;

namespace Ags.Finance.Web.Attachments;

internal sealed record AttachmentBackupEntry(string StorageKey, long SizeBytes, string Sha256);

internal sealed class AttachmentBackup(IServiceScopeFactory scopes, AttachmentStorageOptions options)
{
    public const string IndexFileName = "attachments.json";
    public const string PoolDirectoryName = "attachments";
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void ClearConnections()
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>().Clear();
    }

    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        try { return new Lease(await scope.ServiceProvider.GetRequiredService<IAttachmentCatalog>().AcquireMaintenanceLeaseAsync(cancellationToken), scope); }
        catch { await scope.DisposeAsync(); throw; }
    }

    public async Task<IReadOnlyList<AttachmentBackupEntry>> ReadCurrentAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            return (await scope.ServiceProvider.GetRequiredService<IAttachmentCatalog>().ListAsync(cancellationToken))
                .Select(x => new AttachmentBackupEntry(x.StorageKey, x.SizeBytes, x.Sha256)).ToList();
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        { return []; }
    }

    public async Task CopyToPoolAsync(string backupDirectory, IReadOnlyList<AttachmentBackupEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count > 0 && options.Error is { } error) { throw new IOException(error); }
        var pool = Path.Combine(backupDirectory, PoolDirectoryName);
        foreach (var entry in entries)
        {
            await CopyVerifiedAsync(Resolve(options.Directory, entry.StorageKey), Resolve(pool, entry.StorageKey), entry, cancellationToken);
        }
    }

    public async Task RestoreFilesAsync(string backupDirectory, IReadOnlyList<AttachmentBackupEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0) { return; }
        if (options.Error is not null || !Directory.Exists(options.Directory)) { throw new IOException("Attachment directory is unavailable."); }
        foreach (var entry in entries)
        {
            await CopyVerifiedAsync(Resolve(Path.Combine(backupDirectory, PoolDirectoryName), entry.StorageKey), Resolve(options.Directory, entry.StorageKey), entry, cancellationToken);
        }
    }

    public static async Task WriteIndexAsync(string directory, IReadOnlyList<AttachmentBackupEntry> entries, CancellationToken cancellationToken)
    {
        await using var file = File.Create(Path.Combine(directory, IndexFileName));
        await JsonSerializer.SerializeAsync(file, entries, s_json, cancellationToken);
    }

    public static async Task<IReadOnlyList<AttachmentBackupEntry>> ReadIndexAsync(string directory, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(Path.Combine(directory, IndexFileName));
        var entries = await JsonSerializer.DeserializeAsync<List<AttachmentBackupEntry>>(file, s_json, cancellationToken)
            ?? throw new InvalidDataException("Invalid attachment index.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null || entry.Sha256 is null) { throw new InvalidDataException("Invalid attachment entry."); }
            _ = Resolve(directory, entry.StorageKey);
            if (entry.SizeBytes < 1 || entry.Sha256.Length != 64 || !entry.Sha256.All(Uri.IsHexDigit) || !keys.Add(entry.StorageKey))
            { throw new InvalidDataException("Invalid attachment entry."); }
        }
        return entries;
    }

    public static string Resolve(string directory, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains('\\') || key.Contains(':') || Path.IsPathRooted(key)
            || key.Split('/').Any(x => x is "" or "." or "..")) { throw new InvalidDataException("Invalid attachment path."); }
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        { throw new InvalidDataException("Invalid attachment path."); }
        var current = path;
        while (current.Length > root.Length)
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            { throw new InvalidDataException("Symbolic links are not attachment files."); }
            current = Path.GetDirectoryName(current) ?? root;
        }
        return path;
    }

    public static async Task<bool> MatchesAsync(string path, AttachmentBackupEntry entry, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != entry.SizeBytes) { return false; }
        await using var file = File.OpenRead(path);
        return string.Equals(Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken)), entry.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task CopyVerifiedAsync(string source, string target, AttachmentBackupEntry entry, CancellationToken cancellationToken)
    {
        if (!await MatchesAsync(source, entry, cancellationToken)) { throw new InvalidDataException("Attachment source is missing or corrupt."); }
        if (File.Exists(target))
        {
            if (!await MatchesAsync(target, entry, cancellationToken)) { throw new InvalidDataException("Attachment destination is corrupt."); }
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? throw new IOException());
        var temporary = target + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            { await input.CopyToAsync(output, cancellationToken); }
            if (!await MatchesAsync(temporary, entry, cancellationToken)) { throw new InvalidDataException("Attachment copy is corrupt."); }
            if (!OperatingSystem.IsWindows()) { File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
    }

    private sealed class Lease(IAsyncDisposable lease, AsyncServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await lease.DisposeAsync(); }
            finally { await scope.DisposeAsync(); }
        }
    }
}
