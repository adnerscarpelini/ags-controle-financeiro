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
                if (package.Manifest.Monthly || package == packages[0])
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

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }
}
