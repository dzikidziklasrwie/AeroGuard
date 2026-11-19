using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Data;

public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        DatabasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = true,
        }.ToString();
    }

    public string DatabasePath { get; }

    public async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
