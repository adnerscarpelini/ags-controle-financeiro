using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ags.Finance.Web.Attachments;
using Ags.Finance.Web.Hosting;
using Npgsql;

namespace Ags.Finance.Web.Backup;

/// <summary>Onde estão o banco e os arquivos da aplicação que entram em um backup.</summary>
internal sealed record BackupEnvironment(string ConnectionString, string DataDirectory, string AppVersion)
{
    /// <summary>A versão da aplicação em execução (a do assembly), gravada no manifesto de cada backup.</summary>
    public static string CurrentAppVersion =>
        typeof(BackupEnvironment).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "desconhecida";
}

/// <summary>
/// Backup e restauração do conjunto recuperável: o dump custom do PostgreSQL (todos os schemas, migrations, dados,
/// sequências e vínculos) e os arquivos da aplicação (certificado e chaves de Data Protection; logos estão no dump PostgreSQL).
/// Um backup só existe como pasta final depois de completo e conferido; antes disso vive em uma pasta
/// <c>.partial-*</c> que nunca é listada nem restaurada.
/// </summary>
internal sealed partial class BackupService(
    BackupSettings settings,
    BackupEnvironment environment,
    IPostgresTools tools,
    TimeProvider clock,
    ILogger<BackupService> logger, AttachmentBackup? attachmentFiles = null) : IDisposable
{
    public const string KindManual = "manual";
    public const string KindScheduled = "scheduled";
    public const string KindPreRestore = "pre-restore";
    public const string KindPreUpdate = "pre-update";

    private const string PartialPrefix = ".partial-";
    private const int PublishAttempts = 20;
    private const int ClosingConnectionChecks = 30;
    private static readonly TimeSpan s_publishRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan s_closingConnectionDelay = TimeSpan.FromMilliseconds(100);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Cria um backup novo. Falhas esperadas (pasta ausente, pg_dump com erro) voltam no resultado.</summary>
    public async Task<BackupResult> CreateAsync(string kind, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return BackupResult.Failure("Já existe um backup ou uma restauração em andamento.");
        }

        try
        {
            return await CreateCoreAsync(kind, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Os backups concluídos e legíveis da pasta de backup, do mais novo para o mais antigo.</summary>
    public IReadOnlyList<BackupPackage> List()
    {
        var directory = settings.Directory;
        if (directory is null || !Directory.Exists(directory))
        {
            return [];
        }

        var packages = new List<BackupPackage>();
        foreach (var path in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(path);
            if (name.StartsWith('.'))
            {
                continue;
            }

            var package = TryRead(path);
            if (package is not null)
            {
                packages.Add(package);
            }
        }

        return [.. packages.OrderByDescending(package => package.Manifest.CreatedAt)];
    }

    /// <summary>Confere o pacote: manifesto, arquivos, tamanhos, SHA-256 e se o dump abre. <c>null</c> quando íntegro.</summary>
    public async Task<string?> VerifyAsync(BackupPackage package, CancellationToken cancellationToken)
    {
        try
        {
            return await VerifyCoreAsync(package, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            OperationLog.Failed(logger, "Conferência do backup", exception);
            return "Não foi possível conferir o backup: arquivo ou índice de anexos ausente, inválido ou sem permissão.";
        }
    }

    private async Task<string?> VerifyCoreAsync(BackupPackage package, CancellationToken cancellationToken)
    {
        if (package.Manifest.FormatVersion is < 1 or > BackupManifest.CurrentFormatVersion)
        {
            return $"O backup {package.Name} usa um formato ({package.Manifest.FormatVersion}) que esta versão não conhece.";
        }

        if (package.Manifest.Files.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != package.Manifest.Files.Count)
        { return "O manifesto contém arquivos repetidos."; }
        foreach (var entry in package.Manifest.Files)
        {
            if (entry.Name is not (BackupManifest.DatabaseFileName or BackupManifest.AppFilesName or AttachmentBackup.IndexFileName))
            { return "O manifesto contém um caminho inválido."; }
            var path = Path.Combine(package.Path, entry.Name);
            if (!File.Exists(path))
            {
                return $"O backup {package.Name} está incompleto: falta o arquivo {entry.Name}.";
            }

            if (new FileInfo(path).Length != entry.Bytes || await HashAsync(path, cancellationToken) != entry.Sha256)
            {
                return $"O arquivo {entry.Name} do backup {package.Name} está corrompido (o SHA-256 não confere).";
            }
        }

        if (package.Manifest.Files.All(entry => entry.Name != BackupManifest.DatabaseFileName))
        {
            return $"O backup {package.Name} não contém o dump do banco.";
        }

        if (package.Manifest.Files.All(entry => entry.Name != BackupManifest.AppFilesName)) { return "O backup não contém os arquivos da aplicação."; }
        if (package.Manifest.FormatVersion >= 2)
        {
            if (package.Manifest.Files.All(entry => entry.Name != AttachmentBackup.IndexFileName)) { return "O backup não contém o índice de anexos."; }
            var entries = await AttachmentBackup.ReadIndexAsync(package.Path, cancellationToken);
            foreach (var entry in entries)
            {
                var pool = Path.Combine(settings.Directory ?? Path.GetDirectoryName(package.Path) ?? throw new IOException(), AttachmentBackup.PoolDirectoryName);
                if (!await AttachmentBackup.MatchesAsync(AttachmentBackup.Resolve(pool, entry.StorageKey), entry, cancellationToken))
                { return "O backup contém um anexo ausente ou corrompido (tamanho ou SHA-256 inválido)."; }
            }
        }
        var listing = await tools.ListAsync(Path.Combine(package.Path, BackupManifest.DatabaseFileName), cancellationToken);
        return listing.Succeeded
            ? null
            : $"O dump do backup {package.Name} não pôde ser lido pelo pg_restore: {Summarize(listing.Error)}";
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    internal static int MajorVersion(string version)
    {
        var digits = new string([.. version.TakeWhile(char.IsDigit)]);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ? major : 0;
    }

    /// <summary>Primeiras linhas da saída de erro de uma ferramenta, curtas, sem a senha.</summary>
    internal static string Summarize(string error)
    {
        var lines = error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(3)
            .Select(line => line.Length > 300 ? line[..300] + "…" : line);
        var summary = string.Join(" | ", lines);
        return summary.Length == 0 ? "sem detalhes" : summary;
    }

    private async Task<BackupResult> CreateCoreAsync(string kind, CancellationToken cancellationToken)
    {
        var startedAt = clock.GetUtcNow();
        BackupLog.Started(logger, kind);

        var directoryError = CheckDirectory();
        if (directoryError is not null)
        {
            BackupLog.Failed(logger, kind, directoryError);
            return BackupResult.Failure(directoryError);
        }

        var directory = settings.Directory!;
        var partial = Path.Combine(directory, PartialPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            await using var lease = attachmentFiles is null ? null : await attachmentFiles.AcquireAsync(cancellationToken);
            Directory.CreateDirectory(partial);
            Protect(partial, directory: true);

            var package = await BuildAsync(kind, startedAt, directory, partial, cancellationToken);
            var totalBytes = package.Manifest.Files.Sum(file => file.Bytes);
            BackupLog.Succeeded(logger, kind, package.Name, totalBytes);
            return new BackupResult(package, null);
        }
        catch (BackupFailedException exception)
        {
            BackupLog.Failed(logger, kind, exception.Message);
            return BackupResult.Failure(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NpgsqlException)
        {
            OperationLog.Failed(logger, $"Backup ({kind})", exception);
            return BackupResult.Failure($"Não foi possível concluir o backup: {exception.GetType().Name}.");
        }
        finally
        {
            DeleteQuietly(partial);
        }
    }

    private async Task<BackupPackage> BuildAsync(string kind, DateTimeOffset startedAt, string directory, string partial, CancellationToken cancellationToken)
    {
        var clientVersion = await tools.GetClientVersionAsync(cancellationToken);
        if (clientVersion.Length == 0)
        {
            throw new BackupFailedException("O pg_dump não foi encontrado. Instale o cliente do PostgreSQL ou ajuste Backup:PgDumpPath.");
        }

        var connection = new NpgsqlConnectionStringBuilder(environment.ConnectionString);
        var (serverVersion, migrations) = await ReadServerStateAsync(cancellationToken);
        if (MajorVersion(clientVersion) != MajorVersion(serverVersion))
        {
            throw new BackupFailedException(
                $"O pg_dump {clientVersion} não é da mesma versão principal do servidor PostgreSQL {serverVersion}.");
        }

        var dumpPath = Path.Combine(partial, BackupManifest.DatabaseFileName);
        var dump = await tools.DumpAsync(connection, dumpPath, cancellationToken);
        if (!dump.Succeeded)
        {
            throw new BackupFailedException($"O pg_dump falhou (código {dump.ExitCode}): {Summarize(dump.Error)}");
        }

        var listing = await tools.ListAsync(dumpPath, cancellationToken);
        if (!listing.Succeeded || new FileInfo(dumpPath).Length == 0)
        {
            throw new BackupFailedException($"O dump gerado não passou na conferência do pg_restore: {Summarize(listing.Error)}");
        }

        var attachmentEntries = attachmentFiles is null ? [] : await attachmentFiles.ReadCurrentAsync(cancellationToken);
        if (attachmentFiles is not null) { await attachmentFiles.CopyToPoolAsync(directory, attachmentEntries, cancellationToken); }
        await AttachmentBackup.WriteIndexAsync(partial, attachmentEntries, cancellationToken);

        var archivePath = Path.Combine(partial, BackupManifest.AppFilesName);
        await ArchiveAppFilesAsync(archivePath, cancellationToken);

        var files = new List<BackupFileEntry>();
        foreach (var name in new[] { BackupManifest.DatabaseFileName, BackupManifest.AppFilesName, AttachmentBackup.IndexFileName })
        {
            var path = Path.Combine(partial, name);
            files.Add(new BackupFileEntry(name, new FileInfo(path).Length, await HashAsync(path, cancellationToken)));
        }

        var monthly = !List().Any(existing => existing.Manifest.Monthly && SameMonth(existing.Manifest.CreatedAt, startedAt));
        var manifest = new BackupManifest(
            BackupManifest.CurrentFormatVersion,
            startedAt,
            kind,
            monthly,
            environment.AppVersion,
            connection.Database ?? string.Empty,
            serverVersion,
            clientVersion,
            migrations,
            files);
        var manifestPath = Path.Combine(partial, BackupManifest.FileName);
        await File.WriteAllTextAsync(manifestPath, manifest.ToJson(), Encoding.UTF8, cancellationToken);
        Protect(manifestPath, directory: false);

        var name2 = UniqueName(directory, $"ags-finance-{startedAt.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}-{kind}");
        var finalPath = Path.Combine(directory, name2);
        await PublishAsync(partial, finalPath, cancellationToken);
        return new BackupPackage(name2, finalPath, manifest);
    }

    /// <summary>
    /// Torna o pacote visível renomeando a pasta temporária. No Windows, outro processo (antivírus, indexador) pode estar
    /// lendo os arquivos recém-escritos e recusar a renomeação por instantes; só depois de insistir a falha é real.
    /// </summary>
    internal static async Task PublishAsync(string partial, string finalPath, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(partial, finalPath);
                return;
            }
            catch (Exception exception) when (attempt < PublishAttempts && exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(s_publishRetryDelay, cancellationToken);
            }
        }
    }

    private string? CheckDirectory()
    {
        var directory = settings.Directory;
        if (directory is null)
        {
            return "A pasta de backup não está configurada (Backup:Directory).";
        }

        if (!Directory.Exists(directory))
        {
            return $"A pasta de backup '{directory}' não existe ou não está montada.";
        }

        try
        {
            var probe = Path.Combine(directory, PartialPrefix + "probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(probe);
            Directory.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Sem permissão para gravar na pasta de backup '{directory}'.";
        }

        return null;
    }

    private async Task<(string ServerVersion, IReadOnlyDictionary<string, IReadOnlyList<string>> Migrations)> ReadServerStateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(environment.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        string serverVersion;
        await using (var command = new NpgsqlCommand("SHOW server_version", connection))
        {
            serverVersion = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) ?? string.Empty;
        }

        var schemas = new List<string>();
        await using (var command = new NpgsqlCommand("SELECT table_schema FROM information_schema.tables WHERE table_name = '__ef_migrations' ORDER BY table_schema", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                schemas.Add(reader.GetString(0));
            }
        }

        var migrations = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var schema in schemas)
        {
            var applied = new List<string>();
            var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(schema);
            await using var command = new NpgsqlCommand($"SELECT \"MigrationId\" FROM {quoted}.\"__ef_migrations\" ORDER BY 1", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                applied.Add(reader.GetString(0));
            }

            migrations[schema] = applied;
        }

        return (serverVersion, migrations);
    }

    private async Task ArchiveAppFilesAsync(string archivePath, CancellationToken cancellationToken)
    {
        await using var file = File.Create(archivePath);
        await using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        if (Directory.Exists(environment.DataDirectory))
        {
            await TarFile.CreateFromDirectoryAsync(environment.DataDirectory, gzip, includeBaseDirectory: false, cancellationToken);
        }
        else
        {
            await using var writer = new TarWriter(gzip, leaveOpen: true);
        }
    }

    private static BackupPackage? TryRead(string path)
    {
        var manifestPath = Path.Combine(path, BackupManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = BackupManifest.TryParse(File.ReadAllText(manifestPath));
            return manifest is null ? null : new BackupPackage(Path.GetFileName(path), path, manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool SameMonth(DateTimeOffset first, DateTimeOffset second)
    {
        return first.UtcDateTime.Year == second.UtcDateTime.Year && first.UtcDateTime.Month == second.UtcDateTime.Month;
    }

    private static string UniqueName(string directory, string baseName)
    {
        var name = baseName;
        for (var suffix = 2; Directory.Exists(Path.Combine(directory, name)); suffix++)
        {
            name = $"{baseName}-{suffix}";
        }

        return name;
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    /// <summary>O backup guarda a chave privada do certificado e dados financeiros: só o dono acessa.</summary>
    private static void Protect(string path, bool directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, directory
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Sobra de um backup que falhou; a próxima limpeza tenta de novo. Nunca é listada como backup válido.
        }
    }

    /// <summary>Falha esperada com uma mensagem pronta para o operador.</summary>
    private sealed class BackupFailedException(string message) : Exception(message);
}

internal static partial class BackupLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Backup ({Kind}) iniciado.")]
    public static partial void Started(ILogger logger, string kind);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backup ({Kind}) concluído: {Name}, {Bytes} bytes.")]
    public static partial void Succeeded(ILogger logger, string kind, string name, long bytes);

    [LoggerMessage(Level = LogLevel.Error, Message = "Backup ({Kind}) falhou: {Reason}")]
    public static partial void Failed(ILogger logger, string kind, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retenção aplicada: {Removed} backup(s) antigo(s) removido(s) (limite {Limit}).")]
    public static partial void RetentionApplied(ILogger logger, int removed, int limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restauração do backup {Name} iniciada.")]
    public static partial void RestoreStarted(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restauração do backup {Name} concluída.")]
    public static partial void RestoreSucceeded(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Error, Message = "Restauração do backup {Name} falhou: {Reason}")]
    public static partial void RestoreFailed(ILogger logger, string name, string reason);
}
