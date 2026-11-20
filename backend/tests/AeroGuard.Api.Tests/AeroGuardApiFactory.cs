using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Tests;

public sealed class AeroGuardApiFactory : WebApplicationFactory<Program>
{
    private readonly string _temporaryDirectory;
    private readonly string _adminPassword;
    private readonly bool _syncAdminPassword;
    private readonly bool _ownsTemporaryDirectory;

    public AeroGuardApiFactory()
        : this(
            Path.Combine(Path.GetTempPath(), $"aeroguard-api-tests-{Guid.NewGuid():N}"),
            "Test-Admin-Password-42!",
            false,
            true)
    {
    }

    internal AeroGuardApiFactory(
        string temporaryDirectory,
        string adminPassword,
        bool syncAdminPassword,
        bool ownsTemporaryDirectory = false)
    {
        _temporaryDirectory = temporaryDirectory;
        _adminPassword = adminPassword;
        _syncAdminPassword = syncAdminPassword;
        _ownsTemporaryDirectory = ownsTemporaryDirectory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        builder.UseSetting("AEROGUARD_DATA_DIR", RepositoryPaths.DataDirectory);
        builder.UseSetting(
            "AEROGUARD_DB_PATH",
            Path.Combine(_temporaryDirectory, "aeroguard-tests.db"));
        builder.UseSetting("AEROGUARD_ADMIN_PASSWORD", _adminPassword);
        builder.UseSetting(
            "AEROGUARD_SYNC_ADMIN_PASSWORD",
            _syncAdminPassword.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsTemporaryDirectory && Directory.Exists(_temporaryDirectory))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
