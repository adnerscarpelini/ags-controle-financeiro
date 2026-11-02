using System.Formats.Tar;
using System.IO.Compression;
using Npgsql;

namespace Ags.Finance.Web.Backup;

internal sealed partial class BackupService
{
    /// <summary>
    /// Restaura o backup <paramref name="name"/> sobre o banco configurado. Antes de tocar em qualquer coisa:
    /// confere o pacote e as versões, exige acesso exclusivo ao banco e guarda um backup do estado atual. O banco é
    /// restaurado em uma única transação (apaga os schemas atuais e recria os do backup), então um erro deixa o
    /// estado anterior intacto e nunca há sucesso parcial. Só depois restaura os arquivos da aplicação.
    /// </summary>
    /// <param name="forceDisconnect">Encerra as outras conexões ao banco em vez de recusar.</param>
    public async Task<BackupResult> RestoreAsync(string name, bool forceDisconnect, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return BackupResult.Failure("Já existe um backup ou uma restauração em andamento.");
        }

        try
        {
            BackupLog.RestoreStarted(logger, name);
            var result = await RestoreCoreAsync(name, forceDisconnect, cancellationToken);
            if (result.Succeeded)
            {
                BackupLog.RestoreSucceeded(logger, name);
            }
            else
            {
                // A mensagem pode citar dados da linha que falhou; o log guarda só que falhou.
                BackupLog.RestoreFailed(logger, name, "veja a mensagem devolvida ao operador");
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BackupResult> RestoreCoreAsync(string name, bool forceDisconnect, CancellationToken cancellationToken)
    {
        var package = List().FirstOrDefault(candidate => candidate.Name == name);
        if (package is null)
        {
            return BackupResult.Failure($"O backup '{name}' não foi encontrado na pasta de backup.");
        }

        var integrity = await VerifyAsync(package, cancellationToken);
        if (integrity is not null)
        {
            return BackupResult.Failure(integrity);
        }

        var clientVersion = await tools.GetClientVersionAsync(cancellationToken);
        if (clientVersion.Length == 0)
        {
            return BackupResult.Failure("O pg_restore não foi encontrado. Instale o cliente do PostgreSQL ou ajuste Backup:PgRestorePath.");
        }

        var connection = new NpgsqlConnectionStringBuilder(environment.ConnectionString);
        string serverVersion;
        try
        {
            (serverVersion, _) = await ReadServerStateAsync(cancellationToken);
        }
        catch (NpgsqlException)
        {
            return BackupResult.Failure("Não foi possível conectar ao PostgreSQL para restaurar.");
        }

        var dumpMajor = MajorVersion(package.Manifest.PostgresServerVersion);
        if (MajorVersion(serverVersion) != dumpMajor || MajorVersion(clientVersion) < dumpMajor)
        {
            return BackupResult.Failure(
                $"Versões incompatíveis: o backup foi feito no PostgreSQL {package.Manifest.PostgresServerVersion}; " +
                $"o servidor atual é {serverVersion} e o pg_restore é {clientVersion}. Restaure em uma instalação com a mesma versão principal.");
        }

        var exclusivity = await EnsureExclusiveAsync(forceDisconnect, cancellationToken);
        if (exclusivity is not null)
        {
            return BackupResult.Failure(exclusivity);
        }

        var safety = await CreateCoreAsync(KindPreRestore, cancellationToken);
        if (!safety.Succeeded)
        {
            return BackupResult.Failure($"A restauração foi cancelada porque o backup do estado atual falhou: {safety.Error}");
        }

        NpgsqlConnection.ClearAllPools();
        string preamble;
        try
        {
            preamble = await BuildResetPreambleAsync(cancellationToken);
        }
        catch (NpgsqlException)
        {
            return BackupResult.Failure("Não foi possível preparar a restauração no PostgreSQL. Nada foi alterado.");
        }

        var restore = await tools.RestoreAsync(connection, Path.Combine(package.Path, BackupManifest.DatabaseFileName), preamble, cancellationToken);
        if (!restore.Succeeded)
        {
            return BackupResult.Failure(
                $"A restauração do banco falhou e foi desfeita; o estado anterior continua intacto (backup de segurança: {safety.Package!.Name}). " +
                $"Detalhe: {Summarize(restore.Error)}");
        }

        try
        {
            await RestoreAppFilesAsync(package, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return BackupResult.Failure(
                $"O banco foi restaurado, mas os arquivos da aplicação não ({exception.GetType().Name}). " +
                $"Restaure novamente ou extraia {BackupManifest.AppFilesName} manualmente.");
        }

        return new BackupResult(package, null);
    }

    /// <summary>Recusa a restauração se há outras conexões ao banco, a menos que <paramref name="force"/> as encerre.</summary>
    private async Task<string?> EnsureExclusiveAsync(bool force, CancellationToken cancellationToken)
    {
        // Conexões ociosas do próprio processo, mantidas em pool, não contam como outro acesso ao banco.
        NpgsqlConnection.ClearAllPools();
        try
        {
            await using var connection = new NpgsqlConnection(environment.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var count = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()",
                connection);
            var others = Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            if (others == 0)
            {
                return null;
            }

            if (!force)
            {
                return $"Há {others} outra(s) conexão(ões) ativa(s) ao banco. Pare a aplicação (docker compose stop app) antes de restaurar, " +
                    "ou use --force para encerrá-las.";
            }

            await using var terminate = new NpgsqlCommand(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()",
                connection);
            await terminate.ExecuteNonQueryAsync(cancellationToken);
            return null;
        }
        catch (NpgsqlException)
        {
            return "Não foi possível verificar o acesso exclusivo ao banco.";
        }
    }

    /// <summary>
    /// Comandos que rodam na mesma transação do restore: apagam os schemas existentes do banco (menos os do sistema
    /// e o <c>public</c>), para que um backup antigo não conviva com tabelas de uma versão mais nova.
    /// </summary>
    private async Task<string> BuildResetPreambleAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(environment.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT nspname FROM pg_namespace WHERE nspname NOT IN ('public', 'information_schema') AND nspname NOT LIKE 'pg\\_%' ORDER BY nspname",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var builder = new NpgsqlCommandBuilder();
        var statements = new System.Text.StringBuilder();
        while (await reader.ReadAsync(cancellationToken))
        {
            statements.Append("DROP SCHEMA IF EXISTS ").Append(builder.QuoteIdentifier(reader.GetString(0))).Append(" CASCADE;\n");
        }

        return statements.ToString();
    }

    /// <summary>Extrai o pacote de arquivos para uma pasta temporária e copia por cima da pasta de dados.</summary>
    private async Task RestoreAppFilesAsync(BackupPackage package, CancellationToken cancellationToken)
    {
        var staging = Path.Combine(Path.GetTempPath(), "ags-finance-restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            await using (var file = File.OpenRead(Path.Combine(package.Path, BackupManifest.AppFilesName)))
            await using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                await TarFile.ExtractToDirectoryAsync(gzip, staging, overwriteFiles: true, cancellationToken);
            }

            Directory.CreateDirectory(environment.DataDirectory);
            foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(environment.DataDirectory, Path.GetRelativePath(staging, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: true);
            }
        }
        finally
        {
            DeleteQuietly(staging);
        }
    }
}
