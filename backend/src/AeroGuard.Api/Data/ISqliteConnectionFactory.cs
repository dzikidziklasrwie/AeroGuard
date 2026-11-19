using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Data;

public interface ISqliteConnectionFactory
{
    string DatabasePath { get; }

    Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
