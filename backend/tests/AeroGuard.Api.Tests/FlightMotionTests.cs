using System.Text.Json;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Operations;

namespace AeroGuard.Api.Tests;

public sealed class FlightMotionTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RouteStartsAtSelectedCoordinateAndMovesWithElapsedTime()
    {
        var flight = Flight(durationMs: 45_000);

        var initial = FlightMotion.Calculate(flight, StartedAt);
        var moved = FlightMotion.Calculate(flight, StartedAt.AddSeconds(1));

        Assert.Equal(52.2297, initial.Latitude, 4);
        Assert.Equal(21.0122, initial.Longitude, 4);
        Assert.Equal(0, initial.ScenarioElapsedMs);
        Assert.NotEqual(initial.Latitude, moved.Latitude);
        Assert.InRange(moved.Longitude, -180.0, 180.0);
        Assert.InRange(moved.HeadingDeg, 0.0, 360.0);
    }

    [Fact]
    public void TelemetryClockLoopsAtRecordedScenarioDuration()
    {
        var flight = Flight(durationMs: 45_000);

        var position = FlightMotion.Calculate(flight, StartedAt.AddSeconds(46));

        Assert.Equal(1_000, position.ScenarioElapsedMs);
    }

    [Fact]
    public void AcceptedPolarBoundaryRemainsStableAtPlacementTime()
    {
        var flight = Flight(durationMs: 45_000) with { CenterLatitude = 85.0 };

        var initial = FlightMotion.Calculate(flight, StartedAt);

        Assert.Equal(85.0, initial.Latitude, 4);
    }

    [Fact]
    public void ConditionRequiresSixSubsystemsAndMapsFaultToCritical()
    {
        var healthy = Subsystems()
            .Select(name => Telemetry(name, "HEALTHY", valid: true))
            .ToArray();
        var fault = healthy.ToArray();
        fault[2] = Telemetry("temperature", "FAULT", valid: true);

        Assert.Equal("HEALTHY", FlightCondition.Classify(healthy));
        Assert.Equal("CRITICAL", FlightCondition.Classify(fault));
        Assert.Equal("UNKNOWN", FlightCondition.Classify(healthy[..5]));
    }

    private static FlightRecord Flight(long durationMs) =>
        new(
            Id: 1,
            AircraftId: 2,
            TailNumber: "AG-TEST",
            DisplayName: "Test aircraft",
            AircraftStatus: "ACTIVE",
            OwnerUserId: 3,
            OwnerUsername: "owner",
            OwnerDisplayName: "Owner",
            CurrentUserAccess: "OWNER",
            RunId: 4,
            Scenario: "normal-operation",
            ScenarioLabel: "Normal operation",
            ScenarioDurationMs: durationMs,
            ScenarioTickMs: 100,
            ScopeType: "CONTINENT",
            ScopeCode: "EU",
            CenterLatitude: 52.2297,
            CenterLongitude: 21.0122,
            SpeedKph: 780.0,
            RouteSeed: 42,
            Version: 1,
            PlacedBy: 3,
            StartedAtUtc: StartedAt,
            UpdatedAtUtc: StartedAt);

    private static SubsystemTelemetryDto Telemetry(
        string subsystem,
        string health,
        bool valid) =>
        new(
            subsystem,
            health,
            valid,
            valid ? "VALID" : "MISSING",
            JsonSerializer.SerializeToElement(new { value = 1 }));

    private static string[] Subsystems() =>
    [
        "power",
        "cooling",
        "temperature",
        "communication",
        "computer",
        "equipment",
    ];
}
