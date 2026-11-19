namespace AeroGuard.Api.Data;

public sealed class DatabaseInitializer(
    ISqliteConnectionFactory connectionFactory,
    string schemaPath)
{
    private readonly string _schemaPath = Path.GetFullPath(
        string.IsNullOrWhiteSpace(schemaPath)
            ? throw new ArgumentException("Schema path is required.", nameof(schemaPath))
            : schemaPath);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_schemaPath))
        {
            throw new FileNotFoundException("The database schema file was not found.", _schemaPath);
        }

        var parentDirectory = Path.GetDirectoryName(connectionFactory.DatabasePath)
            ?? throw new InvalidOperationException("The database path has no parent directory.");
        Directory.CreateDirectory(parentDirectory);

        var schema = await File.ReadAllTextAsync(_schemaPath, cancellationToken);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = schema;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
