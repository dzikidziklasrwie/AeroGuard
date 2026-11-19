namespace AeroGuard.Api.Contracts;

public sealed record AircraftDto(
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

public sealed record CreateAircraftRequest(
    string TailNumber,
    string DisplayName,
    string Model,
    string Status,
    string? Notes);

public sealed record UpdateAircraftRequest(
    string TailNumber,
    string DisplayName,
    string Model,
    string Status,
    string? Notes,
    long ExpectedVersion);

public sealed record AircraftAssignmentDto(
    long AircraftId,
    long UserId,
    string Username,
    string DisplayName,
    string AccessLevel,
    long AssignedBy,
    DateTimeOffset AssignedAtUtc);

public sealed record SetAircraftAssignmentRequest(string AccessLevel);
