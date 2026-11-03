using System.Text.Json;

namespace Ags.Finance.Web.Backup;

internal sealed record BackupFileEntry(string Name, long Bytes, string Sha256);

/// <summary>
/// Descreve um backup concluído: quando, com quais versões e quais arquivos (com SHA-256). Fica ao lado do dump.
/// Não guarda credenciais nem dados financeiros.
/// </summary>
internal sealed record BackupManifest(
    int FormatVersion,
    DateTimeOffset CreatedAt,
    string Kind,
    bool Monthly,
    string AppVersion,
    string Database,
    string PostgresServerVersion,
    string PgDumpVersion,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Migrations,
    IReadOnlyList<BackupFileEntry> Files)
{
    public const int CurrentFormatVersion = 2;
    public const string FileName = "manifest.json";
    public const string DatabaseFileName = "database.dump";
    public const string AppFilesName = "app-files.tar.gz";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, s_json);
    }

    public static BackupManifest? TryParse(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(json, s_json);
            return manifest is null || manifest.Files is null || manifest.Migrations is null || manifest.Files.Any(x => x is null) ? null : manifest;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal sealed record BackupPackage(string Name, string Path, BackupManifest Manifest);

/// <summary>Resultado de uma operação de backup ou restauração; falhas esperadas voltam aqui, não como exceção.</summary>
internal sealed record BackupResult(BackupPackage? Package, string? Error)
{
    public bool Succeeded => Error is null;

    public static BackupResult Failure(string error)
    {
        return new BackupResult(null, error);
    }
}
