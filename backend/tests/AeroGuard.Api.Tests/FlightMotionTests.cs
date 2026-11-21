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
        Assert.Equal(FlightRoutePhases.Transit, initial.RoutePhase);
        Assert.Equal(0.0, initial.RouteProgress);
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
    public void AcceptedPolarBoundaryIsInsetForReliableProjection()
    {
        var flight = Flight(durationMs: 45_000) with { CenterLatitude = 85.0 };

        var initial = FlightMotion.Calculate(flight, StartedAt);

        Assert.Equal(82.0, initial.Latitude, 4);
        Assert.Equal(FlightRoutePhases.Transit, initial.RoutePhase);
    }

    [Theory]
    [InlineData("WORLD", 85.0, 180.0)]
    [InlineData("WORLD", -85.0, -180.0)]
    [InlineData("CONTINENT", 84.9999, 179.9999)]
    [InlineData("CONTINENT", -84.9999, -179.9999)]
    [InlineData("COUNTRY", 0.0, 180.0)]
    public void RouteStaysWithinPublishedCoordinateBounds(
        string scopeType,
        double centerLatitude,
        double centerLongitude)
    {
        var flight = Flight(durationMs: 45_000) with
        {
            ScopeType = scopeType,
            CenterLatitude = centerLatitude,
            CenterLongitude = centerLongitude,
        };

        for (var elapsedSeconds = 0; elapsedSeconds <= 43_200; elapsedSeconds += 17)
        {
            var position = FlightMotion.Calculate(
                flight,
                StartedAt.AddSeconds(elapsedSeconds));

            Assert.True(double.IsFinite(position.Latitude));
            Assert.True(double.IsFinite(position.Longitude));
            Assert.True(double.IsFinite(position.HeadingDeg));
            Assert.InRange(position.Latitude, -82.0, 82.0);
            Assert.InRange(position.Longitude, -176.0, 176.0);
            Assert.InRange(position.HeadingDeg, 0.0, 360.0);
            Assert.InRange(position.RouteProgress, 0.0, 1.0);
        }
    }

    [Fact]
    public void RussiaStartReachesSouthAmericaAndThenStaysThere()
    {
        var flight = Flight(durationMs: 45_000) with
        {
            ScopeCode = "SA",
            CenterLatitude = 55.7558,
            CenterLongitude = 37.6173,
        };

        var initial = FlightMotion.Calculate(flight, StartedAt);
        var arrived = FlightMotion.Calculate(flight, StartedAt.AddSeconds(31));
        var later = FlightMotion.Calculate(flight, StartedAt.AddHours(12));
        var region = RouteRegionCatalog.GetContinent("SA");

        Assert.Equal(FlightRoutePhases.Transit, initial.RoutePhase);
        Assert.Equal(FlightRoutePhases.Patrol, arrived.RoutePhase);
        Assert.True(region.Contains(arrived.Latitude, arrived.Longitude));
        Assert.True(region.Contains(later.Latitude, later.Longitude));
    }

    [Theory]
    [InlineData("AF")]
    [InlineData("AN")]
    [InlineData("AS")]
    [InlineData("EU")]
    [InlineData("NA")]
    [InlineData("OC")]
    [InlineData("SA")]
    public void ContinentPatrolRemainsInsideItsPublishedRegion(string scopeCode)
    {
        var flight = Flight(durationMs: 45_000) with
        {
            ScopeCode = scopeCode,
            CenterLatitude = 55.7558,
            CenterLongitude = 37.6173,
        };
        var region = RouteRegionCatalog.GetContinent(scopeCode);

        for (var elapsedSeconds = 31; elapsedSeconds <= 86_431; elapsedSeconds += 19)
        {
            var position = FlightMotion.Calculate(
                flight,
                StartedAt.AddSeconds(elapsedSeconds));

            Assert.Equal(FlightRoutePhases.Patrol, position.RoutePhase);
            Assert.True(
                region.Contains(position.Latitude, position.Longitude),
                $"{scopeCode} route left its region at {elapsedSeconds}s: "
                + $"{position.Latitude}, {position.Longitude}");
        }
    }

    [Fact]
    public void PolandCountryPatrolUsesPolishDestination()
    {
        var poland = RouteRegionCatalog.GetCountry("POL", 52.2297, 21.0122);
        var flight = Flight(durationMs: 45_000) with
        {
            ScopeType = "COUNTRY",
            ScopeCode = "POL",
        };

        var initial = FlightMotion.Calculate(flight, StartedAt);
        var arrived = FlightMotion.Calculate(flight, StartedAt.AddSeconds(31));
        var later = FlightMotion.Calculate(flight, StartedAt.AddHours(12));

        Assert.Equal(FlightRoutePhases.Transit, initial.RoutePhase);
        Assert.Equal(FlightRoutePhases.Patrol, arrived.RoutePhase);
        Assert.InRange(arrived.Latitude, 49.5, 54.5);
        Assert.InRange(arrived.Longitude, 15.0, 23.0);
        Assert.True(poland.Contains(later.Latitude, later.Longitude));
    }

    [Fact]
    public void EuropePatrolNeverReachesAfricanLatitudes()
    {
        var flight = Flight(durationMs: 45_000) with
        {
            CenterLatitude = 52.2297,
            CenterLongitude = 21.0122,
        };

        for (var elapsedSeconds = 31; elapsedSeconds <= 86_431; elapsedSeconds += 23)
        {
            var position = FlightMotion.Calculate(
                flight,
                StartedAt.AddSeconds(elapsedSeconds));

            Assert.InRange(position.Latitude, 44.5, 59.5);
            Assert.True(RouteRegionCatalog.GetContinent("EU").Contains(
                position.Latitude,
                position.Longitude));
        }
    }

    [Fact]
    public void EuropePatrolCannotLeaveEuropeAcrossTheMediterranean()
    {
        var europe = RouteRegionCatalog.GetContinent("EU");
        var flight = Flight(durationMs: 45_000) with
        {
            ScopeCode = "EU",
            CenterLatitude = 52.2297,
            CenterLongitude = 21.0122,
        };

        for (var elapsedSeconds = 31; elapsedSeconds <= 86_431; elapsedSeconds += 29)
        {
            var position = FlightMotion.Calculate(
                flight,
                StartedAt.AddSeconds(elapsedSeconds));

            Assert.True(
                europe.Contains(position.Latitude, position.Longitude),
                $"EU route left Europe at {elapsedSeconds}s: "
                + $"{position.Latitude}, {position.Longitude}");
            Assert.False(
                position.Latitude < 37.0
                && position.Longitude >= -17.0
                && position.Longitude <= 51.0);
        }
    }

    [Fact]
    public void RouteRemainsFiniteAfterDecadesOfElapsedTime()
    {
        var flight = Flight(durationMs: 45_000) with
        {
            ScopeCode = "SA",
            CenterLatitude = 55.7558,
            CenterLongitude = 37.6173,
        };

        var position = FlightMotion.Calculate(flight, StartedAt.AddYears(50));

        Assert.True(double.IsFinite(position.Latitude));
        Assert.True(double.IsFinite(position.Longitude));
        Assert.True(double.IsFinite(position.HeadingDeg));
        Assert.Equal(FlightRoutePhases.Patrol, position.RoutePhase);
        Assert.True(RouteRegionCatalog.GetContinent("SA").Contains(
            position.Latitude,
            position.Longitude));
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
