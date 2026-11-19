using System.Globalization;
using System.Security.Claims;

namespace AeroGuard.Api.Identity;

public sealed record CurrentUser(
    long Id,
    long SessionId,
    string Username,
    IReadOnlySet<string> Roles)
{
    public bool IsInRole(string role) => Roles.Contains(role);

    public static CurrentUser FromPrincipal(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var userId = ParseRequiredLong(principal, ClaimTypes.NameIdentifier);
        var sessionId = ParseRequiredLong(
            principal,
            BearerTokenAuthenticationHandler.SessionIdClaim);
        var username = principal.Identity?.Name
            ?? throw new InvalidOperationException("Authenticated principal has no username.");
        var roles = principal.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new CurrentUser(userId, sessionId, username, roles);
    }

    private static long ParseRequiredLong(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Authenticated principal has no valid {claimType} claim.");
    }
}
