namespace Ags.Finance.Web.Backup;

/// <summary>O último resultado de backup, para a tela e para o diagnóstico. Singleton, seguro entre threads.</summary>
internal sealed class BackupStatus
{
    private readonly Lock _lock = new();
    private DateTimeOffset? _lastAttemptAt;
    private DateTimeOffset? _lastSuccessAt;
    private string? _lastSuccessName;
    private string? _lastError;

    public DateTimeOffset? LastAttemptAt
    {
        get { lock (_lock) { return _lastAttemptAt; } }
    }

    public DateTimeOffset? LastSuccessAt
    {
        get { lock (_lock) { return _lastSuccessAt; } }
    }

    public string? LastSuccessName
    {
        get { lock (_lock) { return _lastSuccessName; } }
    }

    public string? LastError
    {
        get { lock (_lock) { return _lastError; } }
    }

    public void Record(BackupResult result, DateTimeOffset now)
    {
        lock (_lock)
        {
            _lastAttemptAt = now;
            if (result.Succeeded)
            {
                _lastSuccessAt = now;
                _lastSuccessName = result.Package?.Name;
                _lastError = null;
            }
            else
            {
                _lastError = result.Error;
            }
        }
    }
}
