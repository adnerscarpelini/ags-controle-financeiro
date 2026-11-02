using Npgsql;

namespace Ags.Finance.Tests.Support;

/// <summary>
/// Um banco PostgreSQL real, criado vazio para um teste e apagado no fim. O servidor vem da variável de ambiente
/// <c>AGS_TEST_POSTGRES</c>: uma connection string com permissão para criar bancos (veja <c>tests/postgres/compose.yaml</c>).
/// </summary>
public sealed class PostgresTestDatabase : IAsyncDisposable
{
    public const string ServerVariable = "AGS_TEST_POSTGRES";

    private readonly string _adminConnectionString;
    private readonly string _name;

    private PostgresTestDatabase(string adminConnectionString, string name, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _name = name;
        ConnectionString = connectionString;
    }

    /// <summary>Connection string do banco criado para este teste.</summary>
    public string ConnectionString { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var admin = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(admin))
        {
            throw new InvalidOperationException(
                $"Os testes relacionais exigem um PostgreSQL real. Defina {ServerVariable} com a connection string de um servidor de testes "
                + "(por exemplo, o de tests/postgres/compose.yaml).");
        }

        var name = "ags_test_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(admin) { Database = name }.ToString();
        return new PostgresTestDatabase(admin, name, connectionString);
    }

    /// <summary>Primeira coluna de cada linha do resultado, como texto.</summary>
    public async Task<List<string>> QueryAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return values;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string> ScalarAsync(string sql)
    {
        var values = await QueryAsync(sql);
        return values.Count == 0 ? string.Empty : values[0];
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
