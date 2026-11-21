using System.Globalization;
using AeroGuard.Api.Data;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Operations;

public sealed class OperationsRepository(
    ISqliteConnectionFactory connectionFactory)
    : IOperationsRepository
{
    private const string FlightSelect =
        """
        SELECT active_flights.id, aircraft.id, aircraft.tail_number,
               aircraft.display_name, aircraft.status,
               owner_user.id, owner_user.username, owner_user.display_name,
               current_assignment.access_level,
               runs.id, runs.scenario, runs.label, runs.duration_ms, runs.tick_ms,
               active_flights.scope_type, active_flights.scope_code,
               active_flights.center_latitude, active_flights.center_longitude,
               active_flights.speed_kph, active_flights.route_seed,
               active_flights.version, active_flights.placed_by,
               active_flights.started_at_utc, active_flights.updated_at_utc
        FROM active_flights
        JOIN aircraft ON aircraft.id = active_flights.aircraft_id
        JOIN runs ON runs.id = active_flights.run_id
        JOIN aircraft_assignments AS owner_assignment
          ON owner_assignment.aircraft_id = aircraft.id
         AND owner_assignment.access_level = 'OWNER'
         AND owner_assignment.user_id = (
             SELECT candidate.user_id
             FROM aircraft_assignments AS candidate
             WHERE candidate.aircraft_id = aircraft.id
               AND candidate.access_level = 'OWNER'
             ORDER BY candidate.assigned_at_utc, candidate.user_id
             LIMIT 1
         )
        JOIN users AS owner_user ON owner_user.id = owner_assignment.user_id
        LEFT JOIN aircraft_assignments AS current_assignment
          ON current_assignment.aircraft_id = aircraft.id
         AND current_assignment.user_id = $current_user_id
        """;

    public async Task<IReadOnlyList<FlightRecord>> GetVisibleFlightsAsync(
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = FlightSelect + "\n" +
            """
            WHERE $is_admin = 1 OR current_assignment.user_id IS NOT NULL
            ORDER BY aircraft.tail_number COLLATE NOCASE, active_flights.id;
            """;
        AddVisibilityParameters(command, userId, isAdmin);
        return await ReadFlightsAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<FlightRecord>> GetAllFlightsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = FlightSelect + "\nORDER BY active_flights.id;";
        AddVisibilityParameters(command, 0, true);
        return await ReadFlightsAsync(command, cancellationToken);
    }

    public async Task<FlightRecord?> GetFlightAsync(
        long flightId,
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = FlightSelect + "\n" +
            """
            WHERE active_flights.id = $flight_id
              AND ($is_admin = 1 OR current_assignment.user_id IS NOT NULL)
            LIMIT 1;
            """;
        AddVisibilityParameters(command, userId, isAdmin);
        command.Parameters.AddWithValue("$flight_id", flightId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadFlight(reader) : null;
    }

    public async Task<FlightRecord?> GetFlightByAircraftAsync(
        long aircraftId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = FlightSelect + "\n" +
            """
            WHERE active_flights.aircraft_id = $aircraft_id
            LIMIT 1;
            """;
        AddVisibilityParameters(command, 0, true);
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadFlight(reader) : null;
    }

    public async Task<FlightRecord?> CreateFlightAsync(
        long aircraftId,
        long runId,
        string scopeType,
        string scopeCode,
        double centerLatitude,
        double centerLongitude,
        double speedKph,
        long routeSeed,
        long placedBy,
        bool isAdmin,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO active_flights (
                aircraft_id, run_id, scope_type, scope_code,
                center_latitude, center_longitude, speed_kph, route_seed,
                placed_by, started_at_utc, updated_at_utc
            )
            SELECT
                $aircraft_id, $run_id, $scope_type, $scope_code,
                $center_latitude, $center_longitude, $speed_kph, $route_seed,
                $placed_by, $started_at_utc, $updated_at_utc
            FROM aircraft
            WHERE aircraft.id = $aircraft_id
              AND aircraft.status = 'ACTIVE'
              AND (
                  $is_admin = 1
                  OR EXISTS (
                      SELECT 1
                      FROM aircraft_assignments
                      WHERE aircraft_assignments.aircraft_id = aircraft.id
                        AND aircraft_assignments.user_id = $placed_by
                        AND aircraft_assignments.access_level IN ('OWNER', 'MAINTAINER')
                  )
              );
            """;
        AddFlightValues(
            command,
            aircraftId,
            runId,
            scopeType,
            scopeCode,
            centerLatitude,
            centerLongitude,
            speedKph,
            routeSeed);
        command.Parameters.AddWithValue("$placed_by", placedBy);
        command.Parameters.AddWithValue("$is_admin", isAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$started_at_utc", Format(now));
        command.Parameters.AddWithValue("$updated_at_utc", Format(now));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        await using var identity = connection.CreateCommand();
        identity.CommandText = "SELECT last_insert_rowid();";
        var id = Convert.ToInt64(
            await identity.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        return await GetFlightAsync(id, placedBy, true, cancellationToken)
            ?? throw new InvalidOperationException("The stored flight could not be read.");
    }

    public async Task<FlightRecord?> UpdateFlightAsync(
        long flightId,
        long runId,
        string scopeType,
        string scopeCode,
        double centerLatitude,
        double centerLongitude,
        double speedKph,
        long routeSeed,
        long expectedVersion,
        long currentUserId,
        bool isAdmin,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE active_flights
            SET run_id = $run_id,
                scope_type = $scope_type,
                scope_code = $scope_code,
                center_latitude = $center_latitude,
                center_longitude = $center_longitude,
                speed_kph = $speed_kph,
                route_seed = $route_seed,
                version = version + 1,
                started_at_utc = $started_at_utc,
                updated_at_utc = $updated_at_utc
            WHERE id = $flight_id
              AND version = $expected_version
              AND (
                  $is_admin = 1
                  OR EXISTS (
                      SELECT 1
                      FROM aircraft_assignments
                      WHERE aircraft_assignments.aircraft_id = active_flights.aircraft_id
                        AND aircraft_assignments.user_id = $current_user_id
                        AND aircraft_assignments.access_level IN ('OWNER', 'MAINTAINER')
                  )
              );
            """;
        command.Parameters.AddWithValue("$flight_id", flightId);
        command.Parameters.AddWithValue("$expected_version", expectedVersion);
        command.Parameters.AddWithValue("$current_user_id", currentUserId);
        command.Parameters.AddWithValue("$is_admin", isAdmin ? 1 : 0);
        command.Parameters.AddWithValue("$run_id", runId);
        command.Parameters.AddWithValue("$scope_type", scopeType);
        command.Parameters.AddWithValue("$scope_code", scopeCode);
        command.Parameters.AddWithValue("$center_latitude", centerLatitude);
        command.Parameters.AddWithValue("$center_longitude", centerLongitude);
        command.Parameters.AddWithValue("$speed_kph", speedKph);
        command.Parameters.AddWithValue("$route_seed", routeSeed);
        command.Parameters.AddWithValue("$started_at_utc", Format(now));
        command.Parameters.AddWithValue("$updated_at_utc", Format(now));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        return await GetFlightAsync(flightId, currentUserId, isAdmin, cancellationToken);
    }

    public async Task<bool> DeleteFlightAsync(
        long flightId,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM active_flights
            WHERE id = $flight_id
              AND (
                  $is_admin = 1
                  OR EXISTS (
                      SELECT 1
                      FROM aircraft_assignments
                      WHERE aircraft_assignments.aircraft_id = active_flights.aircraft_id
                        AND aircraft_assignments.user_id = $current_user_id
                        AND aircraft_assignments.access_level IN ('OWNER', 'MAINTAINER')
                  )
              );
            """;
        command.Parameters.AddWithValue("$flight_id", flightId);
        command.Parameters.AddWithValue("$current_user_id", currentUserId);
        command.Parameters.AddWithValue("$is_admin", isAdmin ? 1 : 0);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<long>> GetAssignedUserIdsAsync(
        long flightId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT aircraft_assignments.user_id
            FROM active_flights
            JOIN aircraft_assignments
              ON aircraft_assignments.aircraft_id = active_flights.aircraft_id
            JOIN users ON users.id = aircraft_assignments.user_id
            WHERE active_flights.id = $flight_id AND users.is_active = 1
            ORDER BY aircraft_assignments.user_id;
            """;
        command.Parameters.AddWithValue("$flight_id", flightId);
        var userIds = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            userIds.Add(reader.GetInt64(0));
        }

        return userIds;
    }

    public async Task<IReadOnlyDictionary<string, bool>> GetPermissionOverridesAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT permission, enabled
            FROM user_operation_permissions
            WHERE user_id = $user_id
            ORDER BY permission;
            """;
        command.Parameters.AddWithValue("$user_id", userId);
        var permissions = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            permissions[reader.GetString(0)] = reader.GetInt64(1) == 1;
        }

        return permissions;
    }

    public async Task ReplacePermissionOverridesAsync(
        long userId,
        IReadOnlySet<string> enabledPermissions,
        long assignedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = (SqliteTransaction)transaction;
            delete.CommandText = "DELETE FROM user_operation_permissions WHERE user_id = $user_id;";
            delete.Parameters.AddWithValue("$user_id", userId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var permission in OperationPermissionNames.All)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText =
                """
                INSERT INTO user_operation_permissions (
                    user_id, permission, enabled, assigned_by, updated_at_utc
                )
                VALUES ($user_id, $permission, $enabled, $assigned_by, $updated_at_utc);
                """;
            insert.Parameters.AddWithValue("$user_id", userId);
            insert.Parameters.AddWithValue("$permission", permission);
            insert.Parameters.AddWithValue("$enabled", enabledPermissions.Contains(permission) ? 1 : 0);
            insert.Parameters.AddWithValue("$assigned_by", assignedBy);
            insert.Parameters.AddWithValue("$updated_at_utc", Format(now));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FlightMessageRecord>> GetMessagesAsync(
        long flightId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT recent.id, recent.flight_id, recent.sender_user_id,
                   recent.username, recent.display_name, recent.message,
                   recent.share_type, recent.payload_json, recent.sent_at_utc
            FROM (
                SELECT flight_messages.id, flight_messages.flight_id,
                       flight_messages.sender_user_id, users.username,
                       users.display_name, flight_messages.message,
                       flight_messages.share_type, flight_messages.payload_json,
                       flight_messages.sent_at_utc
                FROM flight_messages
                JOIN users ON users.id = flight_messages.sender_user_id
                WHERE flight_messages.flight_id = $flight_id
                ORDER BY flight_messages.sent_at_utc DESC, flight_messages.id DESC
                LIMIT $limit
            ) AS recent
            ORDER BY recent.sent_at_utc, recent.id;
            """;
        command.Parameters.AddWithValue("$flight_id", flightId);
        command.Parameters.AddWithValue("$limit", limit);
        var messages = new List<FlightMessageRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadMessage(reader));
        }

        return messages;
    }

    public async Task<FlightMessageRecord?> AppendMessageAsync(
        long flightId,
        long senderUserId,
        string message,
        string shareType,
        string payloadJson,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO flight_messages (
                flight_id, sender_user_id, message, share_type,
                payload_json, sent_at_utc
            )
            SELECT
                active_flights.id, $sender_user_id, $message, $share_type,
                $payload_json, $sent_at_utc
            FROM active_flights
            WHERE active_flights.id = $flight_id
              AND EXISTS (
                  SELECT 1
                  FROM users
                  WHERE users.id = $sender_user_id
                    AND users.is_active = 1
              )
              AND (
                  EXISTS (
                      SELECT 1
                      FROM user_roles
                      JOIN roles ON roles.id = user_roles.role_id
                      WHERE user_roles.user_id = $sender_user_id
                        AND roles.name = 'Admin' COLLATE NOCASE
                  )
                  OR (
                      EXISTS (
                          SELECT 1
                          FROM aircraft_assignments
                          WHERE aircraft_assignments.aircraft_id = active_flights.aircraft_id
                            AND aircraft_assignments.user_id = $sender_user_id
                      )
                      AND (
                          EXISTS (
                              SELECT 1
                              FROM user_operation_permissions
                              WHERE user_operation_permissions.user_id = $sender_user_id
                                AND user_operation_permissions.permission = 'CHAT_SEND'
                                AND user_operation_permissions.enabled = 1
                          )
                          OR (
                              NOT EXISTS (
                                  SELECT 1
                                  FROM user_operation_permissions
                                  WHERE user_operation_permissions.user_id = $sender_user_id
                              )
                              AND EXISTS (
                                  SELECT 1
                                  FROM user_roles
                                  JOIN roles ON roles.id = user_roles.role_id
                                  WHERE user_roles.user_id = $sender_user_id
                                    AND roles.name IN ('Operator', 'Maintainer', 'Viewer') COLLATE NOCASE
                              )
                          )
                      )
                  )
              )
              AND (
                  $share_type <> 'TELEMETRY'
                  OR EXISTS (
                      SELECT 1
                      FROM user_roles
                      JOIN roles ON roles.id = user_roles.role_id
                      WHERE user_roles.user_id = $sender_user_id
                        AND roles.name = 'Admin' COLLATE NOCASE
                  )
                  OR (
                      EXISTS (
                          SELECT 1
                          FROM aircraft_assignments
                          WHERE aircraft_assignments.aircraft_id = active_flights.aircraft_id
                            AND aircraft_assignments.user_id = $sender_user_id
                            AND aircraft_assignments.access_level IN ('OWNER', 'MAINTAINER')
                      )
                      AND (
                          EXISTS (
                              SELECT 1
                              FROM user_operation_permissions
                              WHERE user_operation_permissions.user_id = $sender_user_id
                                AND user_operation_permissions.permission = 'TELEMETRY_SHARE'
                                AND user_operation_permissions.enabled = 1
                          )
                          OR (
                              NOT EXISTS (
                                  SELECT 1
                                  FROM user_operation_permissions
                                  WHERE user_operation_permissions.user_id = $sender_user_id
                              )
                              AND EXISTS (
                                  SELECT 1
                                  FROM user_roles
                                  JOIN roles ON roles.id = user_roles.role_id
                                  WHERE user_roles.user_id = $sender_user_id
                                    AND roles.name IN ('Operator', 'Maintainer') COLLATE NOCASE
                              )
                          )
                      )
                  )
              )
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$flight_id", flightId);
        command.Parameters.AddWithValue("$sender_user_id", senderUserId);
        command.Parameters.AddWithValue("$message", message);
        command.Parameters.AddWithValue("$share_type", shareType);
        command.Parameters.AddWithValue("$payload_json", payloadJson);
        command.Parameters.AddWithValue("$sent_at_utc", Format(sentAt));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null || result is DBNull)
        {
            return null;
        }

        var id = Convert.ToInt64(result, CultureInfo.InvariantCulture);

        await using var select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT flight_messages.id, flight_messages.flight_id,
                   flight_messages.sender_user_id, users.username,
                   users.display_name, flight_messages.message,
                   flight_messages.share_type, flight_messages.payload_json,
                   flight_messages.sent_at_utc
            FROM flight_messages
            JOIN users ON users.id = flight_messages.sender_user_id
            WHERE flight_messages.id = $id;
            """;
        select.Parameters.AddWithValue("$id", id);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMessage(reader)
            : throw new InvalidOperationException("The stored flight message could not be read.");
    }

    private static void AddVisibilityParameters(
        SqliteCommand command,
        long userId,
        bool isAdmin)
    {
        command.Parameters.AddWithValue("$current_user_id", userId);
        command.Parameters.AddWithValue("$is_admin", isAdmin ? 1 : 0);
    }

    private static void AddFlightValues(
        SqliteCommand command,
        long aircraftId,
        long runId,
        string scopeType,
        string scopeCode,
        double centerLatitude,
        double centerLongitude,
        double speedKph,
        long routeSeed)
    {
        command.Parameters.AddWithValue("$aircraft_id", aircraftId);
        command.Parameters.AddWithValue("$run_id", runId);
        command.Parameters.AddWithValue("$scope_type", scopeType);
        command.Parameters.AddWithValue("$scope_code", scopeCode);
        command.Parameters.AddWithValue("$center_latitude", centerLatitude);
        command.Parameters.AddWithValue("$center_longitude", centerLongitude);
        command.Parameters.AddWithValue("$speed_kph", speedKph);
        command.Parameters.AddWithValue("$route_seed", routeSeed);
    }

    private static async Task<IReadOnlyList<FlightRecord>> ReadFlightsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var flights = new List<FlightRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            flights.Add(ReadFlight(reader));
        }

        return flights;
    }

    private static FlightRecord ReadFlight(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetInt64(9),
            reader.GetString(10),
            reader.GetString(11),
            reader.GetInt64(12),
            reader.GetInt64(13),
            reader.GetString(14),
            reader.GetString(15),
            reader.GetDouble(16),
            reader.GetDouble(17),
            reader.GetDouble(18),
            reader.GetInt64(19),
            reader.GetInt64(20),
            reader.GetInt64(21),
            Parse(reader.GetString(22)),
            Parse(reader.GetString(23)));

    private static FlightMessageRecord ReadMessage(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            Parse(reader.GetString(8)));

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
