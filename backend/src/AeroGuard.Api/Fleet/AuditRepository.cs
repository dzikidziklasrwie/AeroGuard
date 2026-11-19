using System.Globalization;
using System.Text.Json;
using AeroGuard.Api.Data;

namespace AeroGuard.Api.Fleet;

public sealed class AuditRepository(
    ISqliteConnectionFactory connectionFactory)
    : IAuditRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task AppendAsync(
        long? actorUserId,
        string action,
        string entityType,
        string entityId,
        object details,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO audit_log (
                actor_user_id, action, entity_type, entity_id,
                details_json, occurred_at_utc
            )
            VALUES (
                $actor_user_id, $action, $entity_type, $entity_id,
                $details_json, $occurred_at_utc
            );
            """;
        command.Parameters.AddWithValue(
            "$actor_user_id",
            (object?)actorUserId ?? DBNull.Value);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$entity_type", entityType);
        command.Parameters.AddWithValue("$entity_id", entityId);
        command.Parameters.AddWithValue("$details_json", JsonSerializer.Serialize(details, JsonOptions));
        command.Parameters.AddWithValue(
            "$occurred_at_utc",
            occurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditRecord>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT audit_log.id, audit_log.actor_user_id, users.username,
                   audit_log.action, audit_log.entity_type, audit_log.entity_id,
                   audit_log.details_json, audit_log.occurred_at_utc
            FROM audit_log
            LEFT JOIN users ON users.id = audit_log.actor_user_id
            ORDER BY audit_log.occurred_at_utc DESC, audit_log.id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var records = new List<AuditRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new AuditRecord(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? null : reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                DateTimeOffset.Parse(
                    reader.GetString(7),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind)));
        }

        return records;
    }
}
