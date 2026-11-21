namespace AeroGuard.Api.Operations;

public static class FlightMotion
{
    private const double PatrolTimeScale = 360.0;
    private const double TransitTimeScale = 2_400.0;
    private const double EarthRadiusKm = 6_371.0;
    private const double KilometresPerDegree = 111.0;
    private const double MinimumTransitSeconds = 4.0;
    private const double MaximumTransitSeconds = 30.0;
    private const double MaximumRenderedLatitude = 82.0;
    private const double MaximumRenderedLongitude = 176.0;

    public static FlightPosition Calculate(FlightRecord flight, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(flight);
        var elapsedSeconds = Math.Max(0.0, (now - flight.StartedAtUtc).TotalSeconds);
        var elapsedMs = Math.Max(0L, (long)Math.Floor(elapsedSeconds * 1000.0));
        var scenarioElapsedMs = flight.ScenarioDurationMs <= 0
            ? 0
            : elapsedMs % flight.ScenarioDurationMs;

        var start = MakeRenderable(flight.CenterLatitude, flight.CenterLongitude);
        var profile = ResolveProfile(flight, start);
        var entry = MakeRenderable(profile.EntryLatitude, profile.EntryLongitude);
        var transitSeconds = TransitDurationSeconds(start, entry, flight.SpeedKph);
        if (transitSeconds > 0.0 && elapsedSeconds < transitSeconds)
        {
            var progress = Math.Clamp(elapsedSeconds / transitSeconds, 0.0, 1.0);
            var current = MakeRenderable(InterpolateGreatCircle(start, entry, progress));
            var next = MakeRenderable(InterpolateGreatCircle(
                start,
                entry,
                Math.Min(1.0, progress + 0.0001)));
            var heading = InitialBearing(current, next);
            return new FlightPosition(
                current.Latitude,
                current.Longitude,
                heading,
                scenarioElapsedMs,
                FlightRoutePhases.Transit,
                progress);
        }

        var patrolElapsedSeconds = Math.Max(0.0, elapsedSeconds - transitSeconds);
        var patrol = CalculatePatrol(profile, flight, patrolElapsedSeconds);
        return new FlightPosition(
            patrol.Latitude,
            patrol.Longitude,
            patrol.Heading,
            scenarioElapsedMs,
            FlightRoutePhases.Patrol,
            1.0);
    }

    private static RouteRegionProfile ResolveProfile(
        FlightRecord flight,
        Coordinate start) =>
        flight.ScopeType switch
        {
            FlightScopeTypes.Continent => RouteRegionCatalog.GetContinent(flight.ScopeCode),
            FlightScopeTypes.Country => LocalCountryProfile(flight.ScopeCode, start),
            _ => RouteRegionCatalog.World,
        };

    private static RouteRegionProfile LocalCountryProfile(
        string code,
        Coordinate start)
    {
        return RouteRegionCatalog.GetCountry(code, start.Latitude, start.Longitude);
    }

    private static double TransitDurationSeconds(
        Coordinate start,
        Coordinate destination,
        double speedKph)
    {
        var distanceKm = GreatCircleDistanceKm(start, destination);
        if (distanceKm < 1.0)
        {
            return 0.0;
        }

        var simulatedKilometresPerSecond =
            Math.Max(1.0, speedKph) * TransitTimeScale / 3_600.0;
        return Math.Clamp(
            distanceKm / simulatedKilometresPerSecond,
            MinimumTransitSeconds,
            MaximumTransitSeconds);
    }

    private static PatrolPosition CalculatePatrol(
        RouteRegionProfile profile,
        FlightRecord flight,
        double elapsedSeconds)
    {
        var routeRadiusKm = Math.Max(1.0, profile.LatitudeRadius * KilometresPerDegree);
        var direction = flight.RouteSeed % 2 == 0 ? 1.0 : -1.0;
        var rawAngle = direction
            * elapsedSeconds
            * (flight.SpeedKph / 3_600.0)
            * PatrolTimeScale
            / routeRadiusKm;
        var angle = rawAngle % Math.Tau;
        var latitude = profile.CenterLatitude;
        var longitude = profile.CenterLongitude;
        for (var fraction = 1.0; fraction >= 0.0001; fraction *= 0.5)
        {
            latitude = profile.CenterLatitude
                + (Math.Sin(angle) * profile.LatitudeRadius * fraction);
            longitude = NormalizeLongitude(
                profile.CenterLongitude
                + (Math.Cos(angle) * profile.LongitudeRadius * fraction));
            if (profile.Contains(latitude, longitude))
            {
                break;
            }
        }
        var coordinate = MakeRenderable(latitude, longitude);

        var latitudeVelocity = Math.Cos(angle) * profile.LatitudeRadius * direction;
        var longitudeVelocity = -Math.Sin(angle) * profile.LongitudeRadius * direction;
        var heading = NormalizeHeading(
            Math.Atan2(
                longitudeVelocity * Math.Cos(coordinate.Latitude * Math.PI / 180.0),
                latitudeVelocity)
            * 180.0
            / Math.PI);
        return new PatrolPosition(coordinate.Latitude, coordinate.Longitude, heading);
    }

    private static Coordinate InterpolateGreatCircle(
        Coordinate start,
        Coordinate destination,
        double progress)
    {
        if (progress <= 0.0)
        {
            return start;
        }

        if (progress >= 1.0)
        {
            return destination;
        }

        var startVector = ToVector(start);
        var destinationVector = ToVector(destination);
        var dot = Math.Clamp(Dot(startVector, destinationVector), -1.0, 1.0);
        var angle = Math.Acos(dot);
        if (angle < 1e-12)
        {
            return start;
        }

        Vector vector;
        var sinAngle = Math.Sin(angle);
        if (Math.Abs(sinAngle) < 1e-10)
        {
            var reference = Math.Abs(startVector.Z) < 0.9
                ? new Vector(0.0, 0.0, 1.0)
                : new Vector(0.0, 1.0, 0.0);
            var axis = Normalize(Cross(startVector, reference));
            vector = Rotate(startVector, axis, Math.PI * progress);
        }
        else
        {
            var startScale = Math.Sin((1.0 - progress) * angle) / sinAngle;
            var destinationScale = Math.Sin(progress * angle) / sinAngle;
            vector = Normalize(new Vector(
                (startScale * startVector.X) + (destinationScale * destinationVector.X),
                (startScale * startVector.Y) + (destinationScale * destinationVector.Y),
                (startScale * startVector.Z) + (destinationScale * destinationVector.Z)));
        }

        return FromVector(vector);
    }

    private static double GreatCircleDistanceKm(Coordinate start, Coordinate destination)
    {
        var dot = Math.Clamp(Dot(ToVector(start), ToVector(destination)), -1.0, 1.0);
        return Math.Acos(dot) * EarthRadiusKm;
    }

    private static double InitialBearing(Coordinate start, Coordinate destination)
    {
        var latitude1 = start.Latitude * Math.PI / 180.0;
        var latitude2 = destination.Latitude * Math.PI / 180.0;
        var longitudeDelta = LongitudeDifference(
            destination.Longitude,
            start.Longitude) * Math.PI / 180.0;
        var y = Math.Sin(longitudeDelta) * Math.Cos(latitude2);
        var x = (Math.Cos(latitude1) * Math.Sin(latitude2))
            - (Math.Sin(latitude1) * Math.Cos(latitude2) * Math.Cos(longitudeDelta));
        if (Math.Abs(x) < 1e-12 && Math.Abs(y) < 1e-12)
        {
            return 0.0;
        }

        return NormalizeHeading(Math.Atan2(y, x) * 180.0 / Math.PI);
    }

    private static Coordinate MakeRenderable(double latitude, double longitude) =>
        new(
            Math.Clamp(latitude, -MaximumRenderedLatitude, MaximumRenderedLatitude),
            Math.Clamp(
                NormalizeLongitude(longitude),
                -MaximumRenderedLongitude,
                MaximumRenderedLongitude));

    private static Coordinate MakeRenderable(Coordinate coordinate) =>
        MakeRenderable(coordinate.Latitude, coordinate.Longitude);

    private static Vector ToVector(Coordinate coordinate)
    {
        var latitude = coordinate.Latitude * Math.PI / 180.0;
        var longitude = coordinate.Longitude * Math.PI / 180.0;
        var cosLatitude = Math.Cos(latitude);
        return new Vector(
            cosLatitude * Math.Cos(longitude),
            cosLatitude * Math.Sin(longitude),
            Math.Sin(latitude));
    }

    private static Coordinate FromVector(Vector vector)
    {
        var normalized = Normalize(vector);
        var latitude = Math.Asin(Math.Clamp(normalized.Z, -1.0, 1.0)) * 180.0 / Math.PI;
        var longitude = Math.Atan2(normalized.Y, normalized.X) * 180.0 / Math.PI;
        return new Coordinate(latitude, NormalizeLongitude(longitude));
    }

    private static Vector Rotate(Vector vector, Vector axis, double angle)
    {
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        var cross = Cross(axis, vector);
        var dot = Dot(axis, vector);
        return Normalize(new Vector(
            (vector.X * cosine) + (cross.X * sine) + (axis.X * dot * (1.0 - cosine)),
            (vector.Y * cosine) + (cross.Y * sine) + (axis.Y * dot * (1.0 - cosine)),
            (vector.Z * cosine) + (cross.Z * sine) + (axis.Z * dot * (1.0 - cosine))));
    }

    private static Vector Cross(Vector left, Vector right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

    private static double Dot(Vector left, Vector right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private static Vector Normalize(Vector vector)
    {
        var length = Math.Sqrt(Dot(vector, vector));
        return length < 1e-12
            ? new Vector(1.0, 0.0, 0.0)
            : new Vector(vector.X / length, vector.Y / length, vector.Z / length);
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

    private static double LongitudeDifference(double longitude, double reference) =>
        NormalizeLongitude(longitude - reference);

    private static double NormalizeHeading(double heading)
    {
        var normalized = heading % 360.0;
        return normalized < 0.0 ? normalized + 360.0 : normalized;
    }

    private readonly record struct Coordinate(double Latitude, double Longitude);

    private readonly record struct PatrolPosition(
        double Latitude,
        double Longitude,
        double Heading);

    private readonly record struct Vector(double X, double Y, double Z);
}
