namespace AeroGuard.Api.Fleet;

public interface IAircraftRepository
{
    Task<IReadOnlyList<AircraftRecord>> GetVisibleAsync(
        long userId,
        bool includeAll,
        CancellationToken cancellationToken = default);

    Task<AircraftRecord?> GetVisibleAsync(
        long aircraftId,
        long userId,
        bool includeAll,
        CancellationToken cancellationToken = default);

    Task<AircraftRecord?> GetAsync(
        long aircraftId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<AircraftRecord> CreateAsync(
        string tailNumber,
        string displayName,
        string model,
        string status,
        string notes,
        long createdBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<AircraftRecord?> UpdateAsync(
        long aircraftId,
        string tailNumber,
        string displayName,
        string model,
        string status,
        string notes,
        long expectedVersion,
        long currentUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        long aircraftId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AircraftAssignmentRecord>> GetAssignmentsAsync(
        long aircraftId,
        CancellationToken cancellationToken = default);

    Task<AircraftAssignmentRecord> UpsertAssignmentAsync(
        long aircraftId,
        long userId,
        string accessLevel,
        long assignedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAssignmentAsync(
        long aircraftId,
        long userId,
        CancellationToken cancellationToken = default);

    Task<long> CountOwnersAsync(
        long aircraftId,
        CancellationToken cancellationToken = default);
}
