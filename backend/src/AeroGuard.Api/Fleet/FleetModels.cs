namespace AeroGuard.Api.Fleet;

public sealed record AircraftRecord(
    long Id,
    string TailNumber,
    string DisplayName,
    string Model,
    string Status,
    string Notes,
    long Version,
    long CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? CurrentUserAccess);

public sealed record AircraftAssignmentRecord(
    long AircraftId,
    long UserId,
    string Username,
    string DisplayName,
    string AccessLevel,
    long AssignedBy,
    DateTimeOffset AssignedAtUtc);

public sealed record AuditRecord(
    long Id,
    long? ActorUserId,
    string? ActorUsername,
    string Action,
    string EntityType,
    string EntityId,
    string DetailsJson,
    DateTimeOffset OccurredAtUtc);

public static class AircraftStatuses
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "ACTIVE",
        "MAINTENANCE",
        "GROUNDED",
    };
}

public static class AircraftAccessLevels
{
    public const string Owner = "OWNER";
    public const string Maintainer = "MAINTAINER";
    public const string Viewer = "VIEWER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Owner,
        Maintainer,
        Viewer,
    };
}
