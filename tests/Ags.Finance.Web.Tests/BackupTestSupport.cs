using Ags.Finance.Web.Backup;
using Npgsql;

namespace Ags.Finance.Web.Tests;

/// <summary>Relógio manual dos testes de backup, com fuso local configurável.</summary>
internal sealed class FakeBackupClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;

    public override TimeZoneInfo LocalTimeZone => Zone;

    public override DateTimeOffset GetUtcNow()
    {
        return Now;
    }
}

/// <summary>pg_dump, pg_restore e psql simulados: escrevem arquivos pequenos e contam as chamadas.</summary>
internal sealed class FakeBackupTools : IPostgresTools
{
    public string ClientVersion { get; set; } = "18.0";

    public Func<CancellationToken, Task>? AfterDumpAsync { get; set; }
    public Func<CancellationToken, Task>? BeforeRestoreAsync { get; set; }

    public bool DumpFails { get; set; }

    public bool RestoreFails { get; set; }

    public int DumpCalls { get; private set; }

    public int RestoreCalls { get; private set; }

    public string LastPreamble { get; private set; } = string.Empty;

    public Task<string> GetClientVersionAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(ClientVersion);
    }

    public async Task<ToolResult> DumpAsync(NpgsqlConnectionStringBuilder connection, string file, CancellationToken cancellationToken)
    {
        DumpCalls++;
        if (DumpFails)
        {
            await File.WriteAllTextAsync(file, "parcial", cancellationToken);
            return new ToolResult(1, string.Empty, "pg_dump: erro simulado");
        }

        await File.WriteAllTextAsync(file, "PGDMP simulado", cancellationToken);
        if (AfterDumpAsync is not null) { await AfterDumpAsync(cancellationToken); }
        return new ToolResult(0, string.Empty, string.Empty);
    }

    public Task<ToolResult> ListAsync(string file, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ToolResult(0, "listagem", string.Empty));
    }

    public async Task<ToolResult> RestoreAsync(NpgsqlConnectionStringBuilder connection, string file, string preamble, CancellationToken cancellationToken)
    {
        RestoreCalls++;
        LastPreamble = preamble;
        if (BeforeRestoreAsync is not null) { await BeforeRestoreAsync(cancellationToken); }
        return RestoreFails ? new ToolResult(3, string.Empty, "psql: erro simulado") : new ToolResult(0, string.Empty, string.Empty);
    }
}
