namespace AeroGuard.Api.Configuration;

public sealed record AccessOptions(
    string AdminUsername,
    string? AdminPassword,
    bool SyncAdminPassword,
    bool AllowWeakDevelopmentPassword,
    TimeSpan SessionLifetime)
{
    public static AccessOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var username = configuration["AEROGUARD_ADMIN_USERNAME"]?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            username = "admin";
        }

        var allowWeak = bool.TryParse(
            configuration["AEROGUARD_ALLOW_WEAK_DEVELOPMENT_PASSWORD"],
            out var parsedAllowWeak)
            && parsedAllowWeak;
        var syncAdminPassword = bool.TryParse(
            configuration["AEROGUARD_SYNC_ADMIN_PASSWORD"],
            out var parsedSyncAdminPassword)
            && parsedSyncAdminPassword;

        var sessionHours = int.TryParse(
            configuration["AEROGUARD_SESSION_HOURS"],
            out var parsedSessionHours)
            ? parsedSessionHours
            : 8;
        if (sessionHours is < 1 or > 168)
        {
            throw new InvalidOperationException("AEROGUARD_SESSION_HOURS must be from 1 to 168.");
        }

        return new AccessOptions(
            username,
            configuration["AEROGUARD_ADMIN_PASSWORD"],
            syncAdminPassword,
            allowWeak,
            TimeSpan.FromHours(sessionHours));
    }
}
