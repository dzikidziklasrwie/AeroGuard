using System.Globalization;
using System.Text;
using AeroGuard.Api.Models;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Data;

public sealed class ArchiveRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public ArchiveRepository(ISqliteConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    public async Task<ImportDisposition> ImportAsync(
        ValidatedRun run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = "SELECT id FROM runs WHERE source_sha256 = $source_sha256 LIMIT 1;";
            existingCommand.Parameters.AddWithValue("$source_sha256", run.SourceSha256);
            if (await existingCommand.ExecuteScalarAsync(cancellationToken) is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return ImportDisposition.AlreadyPresent;
            }
        }

        var importedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        long runId;

        await using (var insertRun = connection.CreateCommand())
        {
            insertRun.Transaction = transaction;
            insertRun.CommandText =
                """
                INSERT INTO runs (
                    scenario, label, description, source_file, source_sha256,
                    schema_version, seed, tick_ms, duration_ms, final_state,
                    record_count, imported_at_utc, replay_bytes
                )
                VALUES (
                    $scenario, $label, $description, $source_file, $source_sha256,
                    $schema_version, $seed, $tick_ms, $duration_ms, $final_state,
                    $record_count, $imported_at_utc, $replay_bytes
                );
                SELECT last_insert_rowid();
                """;
            insertRun.Parameters.AddWithValue("$scenario", run.Scenario);
            insertRun.Parameters.AddWithValue("$label", run.Label);
            insertRun.Parameters.AddWithValue("$description", run.Description);
            insertRun.Parameters.AddWithValue("$source_file", run.SourceFile);
            insertRun.Parameters.AddWithValue("$source_sha256", run.SourceSha256);
            insertRun.Parameters.AddWithValue("$schema_version", run.SchemaVersion);
            insertRun.Parameters.AddWithValue("$seed", run.Seed);
            insertRun.Parameters.AddWithValue("$tick_ms", run.TickMs);
            insertRun.Parameters.AddWithValue("$duration_ms", run.DurationMs);
            insertRun.Parameters.AddWithValue("$final_state", run.FinalState);
            insertRun.Parameters.AddWithValue("$record_count", run.Records.Count);
            insertRun.Parameters.AddWithValue("$imported_at_utc", importedAt);
            insertRun.Parameters.Add("$replay_bytes", SqliteType.Blob).Value = run.ReplayBytes;

            var result = await insertRun.ExecuteScalarAsync(cancellationToken);
            runId = Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        await using (var insertRecord = connection.CreateCommand())
        {
            insertRecord.Transaction = transaction;
            insertRecord.CommandText =
                """
                INSERT INTO records (
                    run_id, ordinal, record_type, timestamp_ms, severity, subsystem, raw_json
                )
                VALUES (
                    $run_id, $ordinal, $record_type, $timestamp_ms, $severity, $subsystem, $raw_json
                );
                """;

            var runIdParameter = insertRecord.Parameters.Add("$run_id", SqliteType.Integer);
            var ordinalParameter = insertRecord.Parameters.Add("$ordinal", SqliteType.Integer);
            var typeParameter = insertRecord.Parameters.Add("$record_type", SqliteType.Text);
            var timestampParameter = insertRecord.Parameters.Add("$timestamp_ms", SqliteType.Integer);
            var severityParameter = insertRecord.Parameters.Add("$severity", SqliteType.Text);
            var subsystemParameter = insertRecord.Parameters.Add("$subsystem", SqliteType.Text);
            var jsonParameter = insertRecord.Parameters.Add("$raw_json", SqliteType.Text);

            await insertRecord.PrepareAsync(cancellationToken);
            foreach (var record in run.Records)
            {
                runIdParameter.Value = runId;
                ordinalParameter.Value = record.Ordinal;
                typeParameter.Value = record.RecordType;
                timestampParameter.Value = record.TimestampMs;
                severityParameter.Value = (object?)record.Severity ?? DBNull.Value;
                subsystemParameter.Value = (object?)record.Subsystem ?? DBNull.Value;
                jsonParameter.Value = record.RawJson;
                await insertRecord.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return ImportDisposition.Imported;
    }

    public async Task<long> CountRunsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM runs;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<StoredRun>> GetRunsAsync(
        string? scenario,
        string? finalState,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var sql = new StringBuilder(
            """
            SELECT id, schema_version, scenario, label, description, source_file,
                   source_sha256, seed, tick_ms, duration_ms, final_state,
                   record_count, imported_at_utc
            FROM runs
            WHERE 1 = 1
            """);

        if (scenario is not null)
        {
            sql.AppendLine(" AND scenario = $scenario");
            command.Parameters.AddWithValue("$scenario", scenario);
        }

        if (finalState is not null)
        {
            sql.AppendLine(" AND final_state = $final_state");
            command.Parameters.AddWithValue("$final_state", finalState);
        }

        sql.AppendLine(" ORDER BY imported_at_utc DESC, id DESC LIMIT $limit;");
        command.Parameters.AddWithValue("$limit", limit);
        command.CommandText = sql.ToString();

        return await ReadRunsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<StoredRun>> GetLatestScenarioRunsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            WITH ranked AS (
                SELECT id, schema_version, scenario, label, description, source_file,
                       source_sha256, seed, tick_ms, duration_ms, final_state,
                       record_count, imported_at_utc,
                       ROW_NUMBER() OVER (
                           PARTITION BY scenario
                           ORDER BY imported_at_utc DESC, id DESC
                       ) AS row_number
                FROM runs
            )
            SELECT id, schema_version, scenario, label, description, source_file,
                   source_sha256, seed, tick_ms, duration_ms, final_state,
                   record_count, imported_at_utc
            FROM ranked
            WHERE row_number = 1
            ORDER BY scenario, id;
            """;

        return await ReadRunsAsync(command, cancellationToken);
    }

    public async Task<StoredRun?> GetRunAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, schema_version, scenario, label, description, source_file,
                   source_sha256, seed, tick_ms, duration_ms, final_state,
                   record_count, imported_at_utc
            FROM runs
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRun(reader) : null;
    }

    public async Task<byte[]?> GetReplayAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT replay_bytes FROM runs WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as byte[];
    }

    public async Task<IReadOnlyList<StoredRecord>> GetRecordsAsync(
        long runId,
        RecordFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var sql = new StringBuilder(
            """
            SELECT ordinal, record_type, timestamp_ms, severity, subsystem, raw_json
            FROM records
            WHERE run_id = $run_id
            """);
        command.Parameters.AddWithValue("$run_id", runId);

        AppendFilter(sql, command, "record_type", "$record_type", filter.RecordType);
        AppendFilter(sql, command, "severity", "$severity", filter.Severity);
        AppendFilter(sql, command, "subsystem", "$subsystem", filter.Subsystem);

        if (filter.FromMs.HasValue)
        {
            sql.AppendLine(" AND timestamp_ms >= $from_ms");
            command.Parameters.AddWithValue("$from_ms", filter.FromMs.Value);
        }

        if (filter.ToMs.HasValue)
        {
            sql.AppendLine(" AND timestamp_ms <= $to_ms");
            command.Parameters.AddWithValue("$to_ms", filter.ToMs.Value);
        }

        sql.AppendLine(" ORDER BY ordinal ASC LIMIT $limit;");
        command.Parameters.AddWithValue("$limit", filter.Limit);
        command.CommandText = sql.ToString();

        var records = new List<StoredRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new StoredRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5)));
        }

        return records;
    }

    private static void AppendFilter(
        StringBuilder sql,
        SqliteCommand command,
        string column,
        string parameter,
        string? value)
    {
        if (value is null)
        {
            return;
        }

        sql.Append(" AND ").Append(column).Append(" = ").AppendLine(parameter);
        command.Parameters.AddWithValue(parameter, value);
    }

    private static async Task<IReadOnlyList<StoredRun>> ReadRunsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var runs = new List<StoredRun>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runs.Add(ReadRun(reader));
        }

        return runs;
    }

    private static StoredRun ReadRun(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetInt64(7),
            reader.GetInt64(8),
            reader.GetInt64(9),
            reader.GetString(10),
            reader.GetInt64(11),
            DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

}
