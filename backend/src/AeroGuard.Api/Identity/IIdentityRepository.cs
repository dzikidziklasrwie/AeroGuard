namespace AeroGuard.Api.Identity;

public interface IIdentityRepository
{
    Task<UserAccount?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetUserAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<UserAccount> CreateUserAsync(
        string username,
        string displayName,
        string passwordHash,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<UserAccount?> ReplaceRolesAsync(
        long userId,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<UserAccount?> SetUserStatusAsync(
        long userId,
        bool isActive,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> SetPasswordHashAsync(
        long userId,
        string passwordHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleRecord>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<RoleRecord> CreateRoleAsync(
        string name,
        string description,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteRoleAsync(long roleId, CancellationToken cancellationToken = default);

    Task<long> CountActiveAdminsAsync(CancellationToken cancellationToken = default);

    Task<long> CreateSessionAsync(
        string tokenHash,
        long userId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<SessionRecord?> FindSessionAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeSessionAsync(
        long sessionId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default);
}
