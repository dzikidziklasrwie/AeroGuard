using System.Globalization;
using AeroGuard.Api.Data;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Fleet;

public sealed class AircraftRepository(
    ISqliteConnectionFactory connectionFactory)
    : IAircraftRepository
{
    private const string AircraftSelect =
        """
        SELECT aircraft.id, aircraft.tail_number, aircraft.display_name,
               aircraft.model, aircraft.status, aircraft.notes, aircraft.version,
               aircraft.created_by, aircraft.created_at_utc, aircraft.updated_at_utc,
               aircraft_assignments.access_level
        FROM aircraft
        LEFT JOIN aircraft_assignments
          ON aircraft_assignments.aircraft_id = aircraft.id
         AND aircraft_assignments.user_id = $current_user_id
        """;

    public async Task<IReadOnlyList<AircraftRecord>> GetVisibleAsync(
        long userId,
        bool includeAll,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AircraftSelect
            + (includeAll ? string.Empty : " WHERE aircraft_assignments.user_id IS NOT NULL")
            + " ORDER BY aircraft.tail_number COLLATE NOCASE, aircraft.id;";
        command.Parameters.AddWithValue("$current_user_id", userId);

        var aircraft = new List<AircraftRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            aircraft.Add(ReadAircraft(reader));
        }

        return aircraft;
    }

    public async Task<AircraftRecord?> GetVisibleAsync(
        long aircraftId,
        long userId,
        bool includeAll,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AircraftSelect
            + " WHERE aircraft.id = $aircraft_id"
            + (includeAll ? string.Empty : " AND aircraft_assignments.user_id IS NOT NULL")
            + " LIMIT 1;";
        command.Parameters.AddWithValue("$current_user_id", userId);
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);
        return await ReadOneAsync(command, cancellationToken);
    }

    public async Task<AircraftRecord?> GetAsync(
        long aircraftId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = AircraftSelect + " WHERE aircraft.id = $aircraft_id LIMIT 1;";
        command.Parameters.AddWithValue("$current_user_id", userId);
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);
        return await ReadOneAsync(command, cancellationToken);
    }

    public async Task<AircraftRecord> CreateAsync(
        string tailNumber,
        string displayName,
        string model,
        string status,
        string notes,
        long createdBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        long aircraftId;
        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO aircraft (
                    tail_number, display_name, model, status, notes, version,
                    created_by, created_at_utc, updated_at_utc
                )
                VALUES (
                    $tail_number, $display_name, $model, $status, $notes, 1,
                    $created_by, $now, $now
                );
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$tail_number", tailNumber);
            insert.Parameters.AddWithValue("$display_name", displayName);
            insert.Parameters.AddWithValue("$model", model);
            insert.Parameters.AddWithValue("$status", status);
            insert.Parameters.AddWithValue("$notes", notes);
            insert.Parameters.AddWithValue("$created_by", createdBy);
            insert.Parameters.AddWithValue("$now", Format(now));
            aircraftId = Convert.ToInt64(
                await insert.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
        }

        await using (var assignment = connection.CreateCommand())
        {
            assignment.Transaction = transaction;
            assignment.CommandText =
                """
                INSERT INTO aircraft_assignments (
                    aircraft_id, user_id, access_level, assigned_by, assigned_at_utc
                )
                VALUES ($aircraft_id, $user_id, 'OWNER', $assigned_by, $assigned_at_utc);
                """;
            assignment.Parameters.AddWithValue("$aircraft_id", aircraftId);
            assignment.Parameters.AddWithValue("$user_id", createdBy);
            assignment.Parameters.AddWithValue("$assigned_by", createdBy);
            assignment.Parameters.AddWithValue("$assigned_at_utc", Format(now));
            await assignment.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new AircraftRecord(
            aircraftId,
            tailNumber,
            displayName,
            model,
            status,
            notes,
            1,
            createdBy,
            now,
            now,
            AircraftAccessLevels.Owner);
    }

    public async Task<AircraftRecord?> UpdateAsync(
        long aircraftId,
        string tailNumber,
        string displayName,
        string model,
        string status,
        string notes,
        long expectedVersion,
        long currentUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE aircraft
            SET tail_number = $tail_number,
                display_name = $display_name,
                model = $model,
                status = $status,
                notes = $notes,
                version = version + 1,
                updated_at_utc = $updated_at_utc
            WHERE id = $id AND version = $expected_version;
            """;
        command.Parameters.AddWithValue("$tail_number", tailNumber);
        command.Parameters.AddWithValue("$display_name", displayName);
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$notes", notes);
        command.Parameters.AddWithValue("$updated_at_utc", Format(now));
        command.Parameters.AddWithValue("$id", aircraftId);
        command.Parameters.AddWithValue("$expected_version", expectedVersion);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        return await GetAsync(aircraftId, currentUserId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        long aircraftId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM aircraft WHERE id = $id;";
        command.Parameters.AddWithValue("$id", aircraftId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<AircraftAssignmentRecord>> GetAssignmentsAsync(
        long aircraftId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT aircraft_assignments.aircraft_id, users.id, users.username,
                   users.display_name, aircraft_assignments.access_level,
                   aircraft_assignments.assigned_by,
                   aircraft_assignments.assigned_at_utc
            FROM aircraft_assignments
            JOIN users ON users.id = aircraft_assignments.user_id
            WHERE aircraft_assignments.aircraft_id = $aircraft_id
            ORDER BY users.username COLLATE NOCASE, users.id;
            """;
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);

        var assignments = new List<AircraftAssignmentRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(ReadAssignment(reader));
        }

        return assignments;
    }

    public async Task<AircraftAssignmentRecord> UpsertAssignmentAsync(
        long aircraftId,
        long userId,
        string accessLevel,
        long assignedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO aircraft_assignments (
                aircraft_id, user_id, access_level, assigned_by, assigned_at_utc
            )
            VALUES ($aircraft_id, $user_id, $access_level, $assigned_by, $assigned_at_utc)
            ON CONFLICT (aircraft_id, user_id) DO UPDATE SET
                access_level = excluded.access_level,
                assigned_by = excluded.assigned_by,
                assigned_at_utc = excluded.assigned_at_utc;
            """;
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);
        command.Parameters.AddWithValue("$user_id", userId);
        command.Parameters.AddWithValue("$access_level", accessLevel);
        command.Parameters.AddWithValue("$assigned_by", assignedBy);
        command.Parameters.AddWithValue("$assigned_at_utc", Format(now));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT aircraft_assignments.aircraft_id, users.id, users.username,
                   users.display_name, aircraft_assignments.access_level,
                   aircraft_assignments.assigned_by,
                   aircraft_assignments.assigned_at_utc
            FROM aircraft_assignments
            JOIN users ON users.id = aircraft_assignments.user_id
            WHERE aircraft_assignments.aircraft_id = $aircraft_id
              AND aircraft_assignments.user_id = $user_id;
            """;
        select.Parameters.AddWithValue("$aircraft_id", aircraftId);
        select.Parameters.AddWithValue("$user_id", userId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The stored aircraft assignment could not be read.");
        }

        return ReadAssignment(reader);
    }

    public async Task<bool> DeleteAssignmentAsync(
        long aircraftId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM aircraft_assignments
            WHERE aircraft_id = $aircraft_id AND user_id = $user_id;
            """;
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);
        command.Parameters.AddWithValue("$user_id", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<long> CountOwnersAsync(
        long aircraftId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM aircraft_assignments
            WHERE aircraft_id = $aircraft_id AND access_level = 'OWNER';
            """;
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    private static async Task<AircraftRecord?> ReadOneAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAircraft(reader) : null;
    }

    private static AircraftRecord ReadAircraft(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt64(6),
            reader.GetInt64(7),
            Parse(reader.GetString(8)),
            Parse(reader.GetString(9)),
            reader.IsDBNull(10) ? null : reader.GetString(10));

    private static AircraftAssignmentRecord ReadAssignment(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            Parse(reader.GetString(6)));

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
