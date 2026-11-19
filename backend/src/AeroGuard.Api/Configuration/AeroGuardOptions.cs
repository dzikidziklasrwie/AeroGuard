namespace AeroGuard.Api.Configuration;

public sealed record AeroGuardOptions(
    string DatabasePath,
    string DataDirectory,
    string SchemaPath,
    int DefaultRunLimit = 50,
    int MaximumRunLimit = 200,
    int DefaultRecordLimit = 1_000,
    int MaximumRecordLimit = 5_000)
{
    public static AeroGuardOptions FromConfiguration(
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var dataDirectoryValue = configuration["AEROGUARD_DATA_DIR"];
        var databasePathValue = configuration["AEROGUARD_DB_PATH"];

        var dataDirectory = string.IsNullOrWhiteSpace(dataDirectoryValue)
            ? FindLocalDataDirectory(contentRootPath)
            : ResolvePath(contentRootPath, dataDirectoryValue);

        var databasePath = string.IsNullOrWhiteSpace(databasePathValue)
            ? Path.GetFullPath(Path.Combine(contentRootPath, ".data", "aeroguard.db"))
            : ResolvePath(contentRootPath, databasePathValue);

        return new AeroGuardOptions(
            databasePath,
            dataDirectory,
            Path.Combine(AppContext.BaseDirectory, "schema.sql"));
    }

    private static string FindLocalDataDirectory(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.Combine(contentRootPath, "data"),
            Path.Combine(contentRootPath, "frontend", "public", "data"),
            Path.Combine(contentRootPath, "..", "..", "..", "frontend", "public", "data"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "manifest.json")))
            {
                return fullPath;
            }
        }

        return Path.GetFullPath(candidates[0]);
    }

    private static string ResolvePath(string contentRootPath, string value) =>
        Path.GetFullPath(Path.IsPathRooted(value)
            ? value
            : Path.Combine(contentRootPath, value));
}
