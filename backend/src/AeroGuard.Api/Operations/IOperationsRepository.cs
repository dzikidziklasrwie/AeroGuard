namespace AeroGuard.Api.Operations;

public interface IOperationsRepository
{
    Task<IReadOnlyList<FlightRecord>> GetVisibleFlightsAsync(
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlightRecord>> GetAllFlightsAsync(
        CancellationToken cancellationToken = default);

    Task<FlightRecord?> GetFlightAsync(
        long flightId,
        long userId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<FlightRecord?> GetFlightByAircraftAsync(
        long aircraftId,
        CancellationToken cancellationToken = default);

    Task<FlightRecord?> CreateFlightAsync(
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
        CancellationToken cancellationToken = default);

    Task<FlightRecord?> UpdateFlightAsync(
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
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFlightAsync(
        long flightId,
        long currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<long>> GetAssignedUserIdsAsync(
        long flightId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, bool>> GetPermissionOverridesAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task ReplacePermissionOverridesAsync(
        long userId,
        IReadOnlySet<string> enabledPermissions,
        long assignedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlightMessageRecord>> GetMessagesAsync(
        long flightId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<FlightMessageRecord?> AppendMessageAsync(
        long flightId,
        long senderUserId,
        string message,
        string shareType,
        string payloadJson,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken = default);
}
