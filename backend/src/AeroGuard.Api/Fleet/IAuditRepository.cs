namespace AeroGuard.Api.Fleet;

public interface IAuditRepository
{
    Task AppendAsync(
        long? actorUserId,
        string action,
        string entityType,
        string entityId,
        object details,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditRecord>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
