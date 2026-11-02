using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Npgsql;

namespace Ags.Finance.Web.Backup;

internal sealed record ToolResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Fronteira com os programas do cliente PostgreSQL (pg_dump, pg_restore, psql), trocável nos testes.</summary>
internal interface IPostgresTools
{
    /// <summary>Versão do pg_dump instalado (por exemplo, <c>18.6</c>); vazia se o programa não existe.</summary>
    Task<string> GetClientVersionAsync(CancellationToken cancellationToken);

    Task<ToolResult> DumpAsync(NpgsqlConnectionStringBuilder connection, string file, CancellationToken cancellationToken);

    /// <summary>Lista o conteúdo do dump (<c>pg_restore --list</c>): só abre se o arquivo é um dump válido.</summary>
    Task<ToolResult> ListAsync(string file, CancellationToken cancellationToken);

    /// <summary>
    /// Restaura o dump numa única transação: os comandos de <paramref name="preamble"/> rodam antes, na mesma
    /// transação. Qualquer erro desfaz tudo.
    /// </summary>
    Task<ToolResult> RestoreAsync(NpgsqlConnectionStringBuilder connection, string file, string preamble, CancellationToken cancellationToken);
}

/// <summary>
/// Executa os programas do cliente PostgreSQL. A senha vai por <c>PGPASSWORD</c> (nunca por argumento) e a saída
/// de erro é devolvida a quem chamou, que decide o que registrar: ela pode citar dados da linha com falha.
/// </summary>
internal sealed class PostgresTools(BackupSettings settings) : IPostgresTools
{
    public async Task<string> GetClientVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunAsync(settings.PgDumpPath, ["--version"], null, cancellationToken);
            var parts = result.Output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return result.Succeeded && parts.Length >= 3 ? parts[2] : string.Empty;
        }
        catch (Win32Exception)
        {
            return string.Empty;
        }
    }

    public Task<ToolResult> DumpAsync(NpgsqlConnectionStringBuilder connection, string file, CancellationToken cancellationToken)
    {
        return RunAsync(
            settings.PgDumpPath,
            ["--format=custom", "--no-owner", "--no-acl", "--file", file, .. ConnectionArguments(connection)],
            connection.Password,
            cancellationToken);
    }

    public Task<ToolResult> ListAsync(string file, CancellationToken cancellationToken)
    {
        return RunAsync(settings.PgRestorePath, ["--list", file], null, cancellationToken);
    }

    public async Task<ToolResult> RestoreAsync(NpgsqlConnectionStringBuilder connection, string file, string preamble, CancellationToken cancellationToken)
    {
        // pg_restore gera o SQL do dump e o psql o aplica numa transação única, junto do preâmbulo.
        using var restore = Start(settings.PgRestorePath, ["--no-owner", "--no-acl", "--file", "-", file], null, redirectInput: false);
        using var psql = Start(
            settings.PsqlPath,
            ["--no-psqlrc", "--quiet", "--single-transaction", "--set", "ON_ERROR_STOP=1", .. ConnectionArguments(connection)],
            connection.Password,
            redirectInput: true);

        var restoreError = restore.StandardError.ReadToEndAsync(cancellationToken);
        var psqlError = psql.StandardError.ReadToEndAsync(cancellationToken);
        var psqlOutput = psql.StandardOutput.ReadToEndAsync(cancellationToken);

        try
        {
            await psql.StandardInput.WriteAsync(preamble.AsMemory(), cancellationToken);
            await psql.StandardInput.FlushAsync(cancellationToken);
            await restore.StandardOutput.BaseStream.CopyToAsync(psql.StandardInput.BaseStream, cancellationToken);
        }
        catch (IOException)
        {
            // O psql encerrou cedo (erro de SQL); o motivo está na saída de erro dele.
        }
        finally
        {
            psql.StandardInput.Close();
        }

        await restore.WaitForExitAsync(cancellationToken);
        await psql.WaitForExitAsync(cancellationToken);

        var exitCode = restore.ExitCode != 0 ? restore.ExitCode : psql.ExitCode;
        return new ToolResult(exitCode, await psqlOutput, await restoreError + await psqlError);
    }

    private static string[] ConnectionArguments(NpgsqlConnectionStringBuilder connection)
    {
        return
        [
            "--host", connection.Host ?? "localhost",
            "--port", connection.Port.ToString(CultureInfo.InvariantCulture),
            "--username", connection.Username ?? string.Empty,
            "--dbname", connection.Database ?? string.Empty,
        ];
    }

    private static Process Start(string fileName, IEnumerable<string> arguments, string? password, bool redirectInput)
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectInput,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (password is not null)
        {
            info.Environment["PGPASSWORD"] = password;
        }

        info.Environment["PGCONNECT_TIMEOUT"] = "15";
        return Process.Start(info) ?? throw new InvalidOperationException($"Não foi possível iniciar {fileName}.");
    }

    private static async Task<ToolResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? password,
        CancellationToken cancellationToken)
    {
        using var process = Start(fileName, arguments, password, redirectInput: false);
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ToolResult(process.ExitCode, await output, await error);
    }
}
