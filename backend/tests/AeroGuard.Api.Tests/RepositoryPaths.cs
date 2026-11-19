namespace AeroGuard.Api.Tests;

internal static class RepositoryPaths
{
    public static string Root { get; } = FindRoot();

    public static string DataDirectory => Path.Combine(Root, "frontend", "public", "data");

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CMakeLists.txt"))
                && File.Exists(Path.Combine(
                    directory.FullName,
                    "frontend",
                    "public",
                    "data",
                    "manifest.json")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The AeroGuard repository root was not found.");
    }
}
