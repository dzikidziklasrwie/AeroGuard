using System.Text.Json;

namespace AeroGuard.Api.Contracts;

public sealed record SubsystemTelemetryDto(
    string Subsystem,
    string Health,
    bool Valid,
    string Reason,
    JsonElement Data);

public sealed record FlightDto(
    long Id,
    long AircraftId,
    string TailNumber,
    string DisplayName,
    long OwnerUserId,
    string OwnerUsername,
    string OwnerDisplayName,
    string? CurrentUserAccess,
    string ScopeType,
    string ScopeCode,
    double Latitude,
    double Longitude,
    double HeadingDeg,
    double SpeedKph,
    long RunId,
    string Scenario,
    string ScenarioLabel,
    long ScenarioElapsedMs,
    long ScenarioDurationMs,
    string Condition,
    IReadOnlyList<SubsystemTelemetryDto> Telemetry,
    long Version,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool CanManage,
    bool CanRemove,
    long EventSequence);

public sealed record PlaceFlightRequest(
    long AircraftId,
    long RunId,
    string ScopeType,
    string ScopeCode,
    double Latitude,
    double Longitude,
    double SpeedKph);

public sealed record UpdateFlightRequest(
    long AircraftId,
    long RunId,
    string ScopeType,
    string ScopeCode,
    double Latitude,
    double Longitude,
    double SpeedKph,
    long ExpectedVersion);

public sealed record SendFlightMessageRequest(
    string? Message,
    string ShareType);

public sealed record FlightMessageDto(
    long Id,
    long FlightId,
    long SenderUserId,
    string SenderUsername,
    string SenderDisplayName,
    string Message,
    string ShareType,
    JsonElement Payload,
    DateTimeOffset SentAtUtc);

public sealed record OperationPermissionsDto(
    IReadOnlyList<string> Permissions);

public sealed record ReplaceOperationPermissionsRequest(
    IReadOnlyList<string> Permissions);
