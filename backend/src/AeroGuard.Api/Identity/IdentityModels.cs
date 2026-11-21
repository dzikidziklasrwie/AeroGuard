namespace AeroGuard.Api.Identity;

public sealed record UserAccount(
    long Id,
    string Username,
    string DisplayName,
    string PasswordHash,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> Roles);

public sealed record RoleRecord(
    long Id,
    string Name,
    string Description,
    bool IsSystem,
    long UserCount,
    DateTimeOffset CreatedAtUtc);

public sealed record SessionRecord(
    long Id,
    UserAccount User,
    DateTimeOffset ExpiresAtUtc);

public sealed record IssuedAccessToken(
    string Value,
    string Hash,
    DateTimeOffset ExpiresAtUtc);

public static class BuiltInRoles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Maintainer = "Maintainer";
    public const string Viewer = "Viewer";
}
