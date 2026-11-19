using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Tests;

public sealed class AeroGuardApiFactory : WebApplicationFactory<Program>
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"aeroguard-api-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        builder.UseSetting("AEROGUARD_DATA_DIR", RepositoryPaths.DataDirectory);
        builder.UseSetting(
            "AEROGUARD_DB_PATH",
            Path.Combine(_temporaryDirectory, "aeroguard-tests.db"));
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_temporaryDirectory))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
