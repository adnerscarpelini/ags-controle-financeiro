using System.Security.Cryptography;
using Ags.Finance.Modules.Attachments.Application;
using Ags.Finance.Modules.Attachments.Contracts;
using SkiaSharp;

namespace Ags.Finance.Modules.Attachments.Infrastructure;

internal sealed class AttachmentFiles(AttachmentStorageOptions options) : IAttachmentFiles
{
    public bool IsDirectoryAvailable => Directory.Exists(options.Directory);
    public async Task<(long Bytes, string Sha256)> WriteAsync(string temporaryKey, Stream source, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.Directory)) { throw new DirectoryNotFoundException(); }
        await using var target = new FileStream(Resolve(temporaryKey), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > options.MaxFileSizeBytes) { throw new IOException("File size limit exceeded."); }
            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return (total, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }
    public (string ContentType, string Extension)? Identify(string temporaryKey)
    {
        using var file = File.OpenRead(Resolve(temporaryKey));
        Span<byte> header = stackalloc byte[8];
        if (file.Read(header) < 8) { return null; }
        if (header.StartsWith("%PDF-1."u8) || header.StartsWith("%PDF-2."u8))
        {
            file.Seek(Math.Max(0, file.Length - 1024), SeekOrigin.Begin);
            using var reader = new StreamReader(file);
            return reader.ReadToEnd().Contains("%%EOF", StringComparison.Ordinal) ? ("application/pdf", ".pdf") : null;
        }
        using var codec = SKCodec.Create(Resolve(temporaryKey));
        return codec?.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => ("image/jpeg", ".jpg"),
            SKEncodedImageFormat.Png => ("image/png", ".png"),
            SKEncodedImageFormat.Webp => ("image/webp", ".webp"),
            _ => null
        };
    }
    public void Move(string temporaryKey, string storageKey)
    {
        var target = Resolve(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? throw new IOException());
        File.Move(Resolve(temporaryKey), target);
    }
    public void Delete(string storageKey)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) { File.Delete(path); }
    }
    public Stream Open(string storageKey) => new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
    private string Resolve(string key)
    {
        var root = Path.GetFullPath(options.Directory) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(options.Directory, key));
        if (!path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) { throw new IOException("Invalid storage key."); }
        return path;
    }
}
