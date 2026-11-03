using System.Text.Json;
using Ags.Finance.Web.Attachments;

namespace Ags.Finance.Web.Backup;

internal sealed partial class BackupService
{
    /// <summary>
    /// Aplica a retenção: mantém no máximo <see cref="BackupSettings.RetentionDays"/> backups, apagando os mais
    /// antigos que <b>não</b> sejam o mensal preservado (o primeiro backup de cada mês nunca é apagado) nem o mais recente. Só mexe em
    /// pastas que são backups válidos; qualquer outra pasta é deixada em paz. Também remove restos de backups que
    /// falharam (<c>.partial-*</c>), o que é seguro porque nenhum backup roda enquanto esta operação mantém o bloqueio.
    /// </summary>
    /// <returns>Os nomes dos backups apagados.</returns>
    public async Task<IReadOnlyList<string>> ApplyRetentionAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = settings.Directory;
            if (directory is null || !Directory.Exists(directory))
            {
                return [];
            }

            await using var attachmentLease = attachmentFiles is null ? null : await attachmentFiles.AcquireAsync(cancellationToken);
            foreach (var leftover in Directory.EnumerateDirectories(directory, PartialPrefix + "*"))
            {
                DeleteQuietly(leftover);
            }

            var packages = List().ToList();
            var removed = new List<string>();
            foreach (var package in packages.AsEnumerable().Reverse())
            {
                if (packages.Count - removed.Count <= settings.RetentionDays)
                {
                    break;
                }

                // O mensal é preservado e o backup mais recente nunca é apagado, qualquer que seja o limite.
                if (package.Manifest.Monthly || package == packages[0] || package.Manifest.FormatVersion is < 1 or > BackupManifest.CurrentFormatVersion)
                {
                    continue;
                }

                Directory.Delete(package.Path, recursive: true);
                removed.Add(package.Name);
            }

            if (removed.Count > 0)
            {
                BackupLog.RetentionApplied(logger, removed.Count, settings.RetentionDays);
            }

            try { await PruneAttachmentPoolAsync(cancellationToken); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            { BackupLog.AttachmentRetentionSkipped(logger); }
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PruneAttachmentPoolAsync(CancellationToken cancellationToken)
    {
        if (settings.Directory is null) { return; }
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var package in List())
        {
            if (package.Manifest.FormatVersion == 1) { continue; }
            if (package.Manifest.FormatVersion != 2) { return; }
            var index = package.Manifest.Files.SingleOrDefault(x => x.Name == AttachmentBackup.IndexFileName)
                ?? throw new InvalidDataException("Missing attachment index.");
            var indexPath = Path.Combine(package.Path, AttachmentBackup.IndexFileName);
            if (!File.Exists(indexPath) || new FileInfo(indexPath).Length != index.Bytes || await HashAsync(indexPath, cancellationToken) != index.Sha256)
            { throw new InvalidDataException("Invalid attachment index."); }
            foreach (var entry in await AttachmentBackup.ReadIndexAsync(package.Path, cancellationToken)) { referenced.Add(entry.StorageKey); }
        }
        var pool = Path.Combine(settings.Directory, AttachmentBackup.PoolDirectoryName);
        if (!Directory.Exists(pool)) { return; }
        foreach (var path in Directory.EnumerateFiles(pool, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            var key = Path.GetRelativePath(pool, path).Replace('\\', '/');
            if (!referenced.Contains(key)) { File.Delete(AttachmentBackup.Resolve(pool, key)); }
        }
    }
}
