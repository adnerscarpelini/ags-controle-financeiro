using System.Globalization;

namespace Ags.Finance.Web.Backup;

/// <summary>Configuração do backup (<c>Backup:*</c>, variáveis <c>Backup__*</c>).</summary>
internal sealed record BackupSettings(
    string? Directory,
    int RetentionDays,
    bool ScheduleEnabled,
    TimeOnly ScheduleTime,
    string PgDumpPath,
    string PgRestorePath,
    string PsqlPath)
{
    public const int DefaultRetentionDays = 30;
    public const string DefaultScheduleTime = "02:00";

    public static BackupSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Backup");

        var retention = DefaultRetentionDays;
        var rawRetention = section["RetentionDays"];
        if (!string.IsNullOrWhiteSpace(rawRetention)
            && (!int.TryParse(rawRetention, NumberStyles.None, CultureInfo.InvariantCulture, out retention) || retention < 1))
        {
            throw new InvalidOperationException($"Backup:RetentionDays deve ser um número inteiro maior que zero, mas veio '{rawRetention}'.");
        }

        var rawTime = section["ScheduleTime"];
        var time = TimeOnly.Parse(DefaultScheduleTime, CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(rawTime)
            && !TimeOnly.TryParseExact(rawTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            throw new InvalidOperationException($"Backup:ScheduleTime deve estar no formato HH:mm, mas veio '{rawTime}'.");
        }

        var directory = section["Directory"];
        return new BackupSettings(
            string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory),
            retention,
            !string.Equals(section["ScheduleEnabled"], "false", StringComparison.OrdinalIgnoreCase),
            time,
            Tool(section["PgDumpPath"], "pg_dump"),
            Tool(section["PgRestorePath"], "pg_restore"),
            Tool(section["PsqlPath"], "psql"));
    }

    private static string Tool(string? configured, string fallback)
    {
        return string.IsNullOrWhiteSpace(configured) ? fallback : configured;
    }
}
