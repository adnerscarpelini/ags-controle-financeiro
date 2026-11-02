namespace Ags.Finance.Web.Backup;

/// <summary>Quando o backup agendado é devido: a última ocorrência do horário diário já passou sem backup posterior.</summary>
internal static class BackupSchedule
{
    public static DateTimeOffset LastDue(DateTimeOffset now, TimeOnly time, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var candidate = At(today, time, zone);
        return candidate <= now ? candidate : At(today.AddDays(-1), time, zone);
    }

    public static DateTimeOffset NextDue(DateTimeOffset now, TimeOnly time, TimeZoneInfo zone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var candidate = At(today, time, zone);
        return candidate > now ? candidate : At(today.AddDays(1), time, zone);
    }

    private static DateTimeOffset At(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}

/// <summary>
/// Backup diário no horário configurado (<c>Backup:ScheduleTime</c>, no fuso da aplicação). Se o computador estava
/// desligado no horário, o backup devido é feito assim que a aplicação sobe. Falhas são registradas e repetidas em 1 hora.
/// </summary>
internal sealed class BackupScheduler(
    BackupService backups,
    BackupSettings settings,
    BackupStatus status,
    MaintenanceMode maintenance,
    TimeProvider clock,
    ILogger<BackupScheduler> logger) : BackgroundService
{
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);

    private static readonly TimeSpan s_startupDelay = TimeSpan.FromSeconds(30);

    /// <summary>Faz o backup se estiver devido e devolve quanto esperar até a próxima verificação.</summary>
    public async Task<TimeSpan> RunIfDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var zone = clock.LocalTimeZone;
        var due = BackupSchedule.LastDue(now, settings.ScheduleTime, zone);
        var latest = backups.List().Select(package => package.Manifest.CreatedAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();

        if (latest < due && !maintenance.IsActive)
        {
            var result = await backups.CreateAsync(BackupService.KindScheduled, cancellationToken);
            status.Record(result, clock.GetUtcNow());
            if (!result.Succeeded)
            {
                return RetryAfterFailure;
            }

            await backups.ApplyRetentionAsync(cancellationToken);
        }

        return BackupSchedule.NextDue(clock.GetUtcNow(), settings.ScheduleTime, zone) - clock.GetUtcNow();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.ScheduleEnabled || settings.Directory is null)
        {
            SchedulerLog.Disabled(logger);
            return;
        }

        try
        {
            await Task.Delay(s_startupDelay, clock, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                var wait = await RunIfDueAsync(stoppingToken);
                await Task.Delay(wait, clock, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Encerramento da aplicação.
        }
    }
}

internal static partial class SchedulerLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Backup agendado desativado (sem Backup:Directory ou Backup:ScheduleEnabled=false).")]
    public static partial void Disabled(ILogger logger);
}
