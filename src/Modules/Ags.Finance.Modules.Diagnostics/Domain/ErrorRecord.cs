namespace Ags.Finance.Modules.Diagnostics.Domain;

/// <summary>
/// Um erro inesperado, identificado pela impressão digital. Repetições somam em <see cref="Occurrences"/>.
/// A mensagem da exceção não existe aqui de propósito: ela pode conter valores do usuário.
/// </summary>
internal sealed class ErrorRecord : Entity
{
    public const int RetentionDays = 90;
    public const int FingerprintLength = 64;
    public const int MaxExceptionTypeLength = 500;
    public const int MaxSourceLength = 500;
    public const int MaxSqlStateLength = 5;

    private ErrorRecord()
    {
        Fingerprint = string.Empty;
        ExceptionType = string.Empty;
        Source = string.Empty;
        StackTrace = string.Empty;
    }

    public string Fingerprint { get; private set; }

    public string ExceptionType { get; private set; }

    public string Source { get; private set; }

    public string StackTrace { get; private set; }

    public string? SqlState { get; private set; }

    public DateTimeOffset FirstOccurredAt { get; private set; }

    public DateTimeOffset LastOccurredAt { get; private set; }

    public long Occurrences { get; private set; }

    public static ErrorRecord Create(string fingerprint, string exceptionType, string source, string stackTrace, string? sqlState, DateTimeOffset occurredAt)
    {
        return new ErrorRecord
        {
            Fingerprint = fingerprint,
            ExceptionType = Limit(exceptionType, MaxExceptionTypeLength),
            Source = Limit(source, MaxSourceLength),
            StackTrace = stackTrace,
            SqlState = sqlState is { Length: <= MaxSqlStateLength } ? sqlState : null,
            FirstOccurredAt = occurredAt,
            LastOccurredAt = occurredAt,
            Occurrences = 1,
        };
    }

    public void Recur(DateTimeOffset occurredAt)
    {
        Occurrences++;
        if (occurredAt > LastOccurredAt)
        {
            LastOccurredAt = occurredAt;
        }
    }

    private static string Limit(string value, int length) => value.Length <= length ? value : value[..length];
}
