namespace AeroGuard.Api.Operations;

public sealed record FlightRecord(
    long Id,
    long AircraftId,
    string TailNumber,
    string DisplayName,
    string AircraftStatus,
    long OwnerUserId,
    string OwnerUsername,
    string OwnerDisplayName,
    string? CurrentUserAccess,
    long RunId,
    string Scenario,
    string ScenarioLabel,
    long ScenarioDurationMs,
    long ScenarioTickMs,
    string ScopeType,
    string ScopeCode,
    double CenterLatitude,
    double CenterLongitude,
    double SpeedKph,
    long RouteSeed,
    long Version,
    long PlacedBy,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record FlightMessageRecord(
    long Id,
    long FlightId,
    long SenderUserId,
    string SenderUsername,
    string SenderDisplayName,
    string Message,
    string ShareType,
    string PayloadJson,
    DateTimeOffset SentAtUtc);

public sealed record FlightPosition(
    double Latitude,
    double Longitude,
    double HeadingDeg,
    long ScenarioElapsedMs);

public sealed record FlightRecipients(
    long FlightId,
    IReadOnlyList<long> UserIds);

public static class FlightScopeTypes
{
    public const string World = "WORLD";
    public const string Continent = "CONTINENT";
    public const string Country = "COUNTRY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        World,
        Continent,
        Country,
    };
}

public static class FlightShareTypes
{
    public const string Text = "TEXT";
    public const string Location = "LOCATION";
    public const string Telemetry = "TELEMETRY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Text,
        Location,
        Telemetry,
    };
}

public static class OperationPermissionNames
{
    public const string FlightPlaceOwn = "FLIGHT_PLACE_OWN";
    public const string FlightRemoveOwn = "FLIGHT_REMOVE_OWN";
    public const string FlightReconfigureOwn = "FLIGHT_RECONFIGURE_OWN";
    public const string ChatSend = "CHAT_SEND";
    public const string TelemetryShare = "TELEMETRY_SHARE";

    public static readonly IReadOnlyList<string> All =
    [
        FlightPlaceOwn,
        FlightRemoveOwn,
        FlightReconfigureOwn,
        ChatSend,
        TelemetryShare,
    ];
}
