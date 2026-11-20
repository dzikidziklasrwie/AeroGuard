namespace AeroGuard.Api.Operations;

public static class FlightMotion
{
    private const double MotionTimeScale = 120.0;
    private const double KilometresPerDegree = 111.0;

    public static FlightPosition Calculate(FlightRecord flight, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(flight);
        var elapsedSeconds = Math.Max(0.0, (now - flight.StartedAtUtc).TotalSeconds);
        var elapsedMs = Math.Max(0L, (long)Math.Floor(elapsedSeconds * 1000.0));
        var scenarioElapsedMs = flight.ScenarioDurationMs <= 0
            ? 0
            : elapsedMs % flight.ScenarioDurationMs;

        var (latitudeRadius, longitudeRadius) = flight.ScopeType switch
        {
            FlightScopeTypes.Country => (2.8, 4.5),
            FlightScopeTypes.Continent => (11.0, 20.0),
            _ => (34.0, 78.0),
        };
        var routeRadiusKm = Math.Max(1.0, latitudeRadius * KilometresPerDegree);
        var direction = flight.RouteSeed % 2 == 0 ? 1.0 : -1.0;
        var angle = direction
            * elapsedSeconds
            * (flight.SpeedKph / 3600.0)
            * MotionTimeScale
            / routeRadiusKm;
        var latitude = Math.Clamp(
            flight.CenterLatitude + (Math.Sin(angle) * latitudeRadius),
            -85.0,
            85.0);
        var longitude = NormalizeLongitude(
            flight.CenterLongitude + ((Math.Cos(angle) - 1.0) * longitudeRadius));

        var latitudeVelocity = Math.Cos(angle) * latitudeRadius * direction;
        var longitudeVelocity = -Math.Sin(angle) * longitudeRadius * direction;
        var heading = NormalizeHeading(
            Math.Atan2(
                longitudeVelocity * Math.Cos(latitude * Math.PI / 180.0),
                latitudeVelocity)
            * 180.0
            / Math.PI);
        return new FlightPosition(latitude, longitude, heading, scenarioElapsedMs);
    }

    private static double NormalizeLongitude(double longitude)
    {
        var normalized = (longitude + 180.0) % 360.0;
        if (normalized < 0.0)
        {
            normalized += 360.0;
        }

        return normalized - 180.0;
    }

    private static double NormalizeHeading(double heading)
    {
        var normalized = heading % 360.0;
        return normalized < 0.0 ? normalized + 360.0 : normalized;
    }
}
