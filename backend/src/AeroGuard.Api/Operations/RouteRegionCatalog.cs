namespace AeroGuard.Api.Operations;

public sealed record RouteRegionProfile(
    string Code,
    string Label,
    double CenterLatitude,
    double CenterLongitude,
    double LatitudeRadius,
    double LongitudeRadius)
{
    public double EntryLatitude => CenterLatitude;

    public double EntryLongitude => CenterLongitude + LongitudeRadius;

    public bool Contains(double latitude, double longitude, double tolerance = 1e-9)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude))
        {
            return false;
        }

        var polygon = RouteRegionCatalog.GetBoundaryPolygon(Code);
        if (polygon is not null)
        {
            return RouteRegionCatalog.ContainsPolygon(
                latitude,
                longitude,
                polygon,
                CenterLongitude,
                tolerance);
        }

        var latitudeOffset = (latitude - CenterLatitude) / LatitudeRadius;
        var longitudeOffset = LongitudeDifference(longitude, CenterLongitude) / LongitudeRadius;
        return (latitudeOffset * latitudeOffset) + (longitudeOffset * longitudeOffset)
            <= 1.0 + tolerance;
    }

    private static double LongitudeDifference(double longitude, double reference)
    {
        var difference = (longitude - reference + 180.0) % 360.0;
        if (difference < 0.0)
        {
            difference += 360.0;
        }

        return difference - 180.0;
    }
}

public static class RouteRegionCatalog
{
    private static readonly RouteRegionProfile WorldProfile =
        new("WORLD", "World", 0.0, 0.0, 55.0, 168.0);

    private static readonly IReadOnlyDictionary<string, RouteRegionProfile> ContinentProfiles =
        new Dictionary<string, RouteRegionProfile>(StringComparer.Ordinal)
        {
            ["AF"] = new("AF", "Africa", 1.0, 20.0, 18.0, 34.0),
            ["AN"] = new("AN", "Antarctica", -80.0, 0.0, 2.5, 55.0),
            ["AS"] = new("AS", "Asia", 40.0, 85.0, 10.0, 28.0),
            ["EU"] = new("EU", "Europe", 52.0, 15.0, 7.5, 18.0),
            ["NA"] = new("NA", "North America", 40.0, -100.0, 12.0, 23.0),
            ["OC"] = new("OC", "Oceania", -25.0, 134.0, 7.5, 12.0),
            ["SA"] = new("SA", "South America", -17.0, -60.0, 15.0, 10.0),
        };

    private static readonly IReadOnlyDictionary<string, RouteRegionProfile> CountryProfiles =
        new Dictionary<string, RouteRegionProfile>(StringComparer.Ordinal)
        {
            ["ALB"] = new("ALB", "Albania", 41.15, 20.0, 1.4, 1.2),
            ["AUT"] = new("AUT", "Austria", 47.5, 14.2, 1.5, 2.2),
            ["BEL"] = new("BEL", "Belgium", 50.5, 4.6, 1.0, 1.5),
            ["BLR"] = new("BLR", "Belarus", 53.7, 27.9, 2.0, 3.0),
            ["BGR"] = new("BGR", "Bulgaria", 42.7, 25.3, 1.6, 2.5),
            ["BIH"] = new("BIH", "Bosnia and Herzegovina", 44.2, 17.8, 1.6, 1.6),
            ["CHE"] = new("CHE", "Switzerland", 46.8, 8.2, 1.2, 1.8),
            ["CZE"] = new("CZE", "Czechia", 49.8, 15.5, 1.3, 2.0),
            ["DEU"] = new("DEU", "Germany", 51.2, 10.4, 2.8, 3.0),
            ["DNK"] = new("DNK", "Denmark", 56.2, 9.4, 1.5, 2.0),
            ["ESP"] = new("ESP", "Spain", 40.2, -3.7, 3.5, 4.0),
            ["EST"] = new("EST", "Estonia", 58.6, 25.0, 1.2, 1.8),
            ["FIN"] = new("FIN", "Finland", 64.0, 26.0, 4.0, 4.0),
            ["FRA"] = new("FRA", "France", 46.8, 2.2, 3.5, 4.0),
            ["GBR"] = new("GBR", "United Kingdom", 54.5, -2.5, 4.0, 3.5),
            ["GRC"] = new("GRC", "Greece", 39.0, 22.0, 2.5, 3.5),
            ["HRV"] = new("HRV", "Croatia", 45.1, 16.0, 2.0, 2.0),
            ["HUN"] = new("HUN", "Hungary", 47.2, 19.5, 1.2, 1.8),
            ["IRL"] = new("IRL", "Ireland", 53.2, -8.0, 2.0, 2.5),
            ["ISL"] = new("ISL", "Iceland", 65.0, -18.6, 2.0, 4.0),
            ["ITA"] = new("ITA", "Italy", 42.8, 12.8, 3.5, 3.5),
            ["LTU"] = new("LTU", "Lithuania", 55.2, 23.9, 1.0, 1.5),
            ["LVA"] = new("LVA", "Latvia", 56.8, 24.6, 1.0, 1.5),
            ["MDA"] = new("MDA", "Moldova", 47.2, 28.6, 1.0, 1.2),
            ["MKD"] = new("MKD", "North Macedonia", 41.6, 21.7, 1.0, 1.0),
            ["MNE"] = new("MNE", "Montenegro", 42.7, 19.3, 0.8, 0.8),
            ["NLD"] = new("NLD", "Netherlands", 52.2, 5.3, 1.2, 1.8),
            ["NOR"] = new("NOR", "Norway", 61.2, 8.5, 4.0, 5.0),
            ["POL"] = new("POL", "Poland", 52.0, 19.0, 2.2, 4.0),
            ["PRT"] = new("PRT", "Portugal", 39.5, -8.0, 2.5, 2.2),
            ["ROU"] = new("ROU", "Romania", 45.9, 25.0, 2.0, 3.0),
            ["SRB"] = new("SRB", "Serbia", 44.0, 21.0, 1.5, 1.8),
            ["SVK"] = new("SVK", "Slovakia", 48.7, 19.5, 1.0, 1.8),
            ["SVN"] = new("SVN", "Slovenia", 46.1, 14.9, 0.8, 1.0),
            ["SWE"] = new("SWE", "Sweden", 62.0, 15.0, 5.0, 4.5),
            ["UKR"] = new("UKR", "Ukraine", 49.0, 31.0, 3.5, 6.0),
            ["CAN"] = new("CAN", "Canada", 56.0, -106.0, 10.0, 20.0),
            ["MEX"] = new("MEX", "Mexico", 23.6, -102.5, 5.0, 8.0),
            ["USA"] = new("USA", "United States", 39.0, -98.0, 8.0, 20.0),
            ["BRA"] = new("BRA", "Brazil", -10.0, -52.0, 10.0, 13.0),
            ["ARG"] = new("ARG", "Argentina", -38.0, -64.0, 8.0, 6.0),
            ["CHL"] = new("CHL", "Chile", -35.0, -71.0, 8.0, 2.0),
            ["COL"] = new("COL", "Colombia", 4.5, -74.0, 4.0, 4.0),
            ["PER"] = new("PER", "Peru", -9.0, -75.0, 5.0, 4.0),
            ["DZA"] = new("DZA", "Algeria", 28.0, 2.0, 7.0, 10.0),
            ["EGY"] = new("EGY", "Egypt", 27.0, 30.0, 5.0, 7.0),
            ["MAR"] = new("MAR", "Morocco", 31.8, -7.0, 4.0, 4.0),
            ["NGA"] = new("NGA", "Nigeria", 9.0, 8.0, 4.0, 4.0),
            ["ZAF"] = new("ZAF", "South Africa", -29.0, 24.0, 5.0, 6.0),
            ["CHN"] = new("CHN", "China", 35.0, 104.0, 12.0, 18.0),
            ["IND"] = new("IND", "India", 22.0, 79.0, 8.0, 8.0),
            ["IDN"] = new("IDN", "Indonesia", -2.0, 118.0, 5.0, 15.0),
            ["JPN"] = new("JPN", "Japan", 37.0, 138.0, 5.0, 5.0),
            ["KOR"] = new("KOR", "South Korea", 36.0, 128.0, 2.0, 2.0),
            ["SAU"] = new("SAU", "Saudi Arabia", 23.0, 45.0, 5.0, 8.0),
            ["TUR"] = new("TUR", "Turkey", 39.0, 35.0, 3.0, 6.0),
            ["AUS"] = new("AUS", "Australia", -25.0, 134.0, 12.0, 16.0),
            ["NZL"] = new("NZL", "New Zealand", -41.0, 174.0, 4.0, 3.0),
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(double Latitude, double Longitude)>> BoundaryPolygons =
        new Dictionary<string, IReadOnlyList<(double Latitude, double Longitude)>>(StringComparer.Ordinal)
        {
            ["AF"] =
            [
                (37.0, -17.0), (37.0, 10.0), (32.0, 32.0), (31.0, 51.0),
                (12.0, 51.0), (-12.0, 40.0), (-35.0, 19.0), (-35.0, 11.0),
                (-20.0, 12.0), (-5.0, 8.0), (5.0, -17.0),
            ],
            ["EU"] =
            [
                (71.0, -10.0), (71.0, 30.0), (66.0, 40.0), (55.0, 60.0),
                (45.0, 40.0), (36.0, 22.0), (36.0, -9.0), (43.0, -10.0),
                (51.0, -5.0), (58.0, 5.0),
            ],
            ["AS"] =
            [
                (77.0, 25.0), (77.0, 180.0), (55.0, 180.0), (35.0, 145.0),
                (20.0, 120.0), (6.0, 100.0), (20.0, 78.0), (25.0, 55.0),
                (35.0, 35.0), (55.0, 25.0),
            ],
            ["NA"] =
            [
                (72.0, -168.0), (72.0, -52.0), (55.0, -52.0), (25.0, -80.0),
                (15.0, -110.0), (30.0, -125.0), (55.0, -140.0),
            ],
            ["SA"] =
            [
                (12.0, -78.0), (12.0, -34.0), (-5.0, -35.0), (-30.0, -48.0),
                (-56.0, -68.0), (-30.0, -72.0),
            ],
            ["OC"] =
            [
                (-10.0, 112.0), (-10.0, 155.0), (-30.0, 155.0),
                (-45.0, 115.0), (-30.0, 105.0),
            ],
        };

    public static RouteRegionProfile World => WorldProfile;

    public static IEnumerable<RouteRegionProfile> Continents =>
        ContinentProfiles.Values;

    public static RouteRegionProfile GetContinent(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return ContinentProfiles.TryGetValue(code, out var profile)
            ? profile
            : throw new ArgumentOutOfRangeException(
                nameof(code),
                code,
                "The continent route code is not supported.");
    }

    public static RouteRegionProfile GetCountry(
        string code,
        double fallbackLatitude,
        double fallbackLongitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (CountryProfiles.TryGetValue(code, out var profile))
        {
            return profile;
        }

        var centerLatitude = Math.Clamp(fallbackLatitude, -78.0, 78.0);
        var centerLongitude = Math.Clamp(fallbackLongitude, -176.0, 176.0);
        return new RouteRegionProfile(
            code,
            code,
            centerLatitude,
            centerLongitude,
            1.5,
            2.5);
    }

    internal static IReadOnlyList<(double Latitude, double Longitude)>? GetBoundaryPolygon(string code) =>
        BoundaryPolygons.TryGetValue(code, out var polygon) ? polygon : null;

    internal static bool ContainsPolygon(
        double latitude,
        double longitude,
        IReadOnlyList<(double Latitude, double Longitude)> polygon,
        double referenceLongitude,
        double tolerance)
    {
        var normalizedLongitude = NormalizeLongitudeForPolygon(longitude, referenceLongitude);
        var inside = false;
        for (var index = 0; index < polygon.Count; index++)
        {
            var start = polygon[index];
            var end = polygon[(index + 1) % polygon.Count];
            var startLongitude = NormalizeLongitudeForPolygon(start.Longitude, referenceLongitude);
            var endLongitude = NormalizeLongitudeForPolygon(end.Longitude, referenceLongitude);
            var crosses = (startLatitude(start) > latitude) != (startLatitude(end) > latitude);
            if (crosses)
            {
                var intersection = ((endLongitude - startLongitude)
                    * (latitude - start.Latitude)
                    / (end.Latitude - start.Latitude))
                    + startLongitude;
                if (normalizedLongitude < intersection)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    private static double startLatitude((double Latitude, double Longitude) point) => point.Latitude;

    private static double NormalizeLongitudeForPolygon(double longitude, double reference)
    {
        var difference = (longitude - reference + 180.0) % 360.0;
        if (difference < 0.0)
        {
            difference += 360.0;
        }

        return reference + (difference - 180.0);
    }
}
