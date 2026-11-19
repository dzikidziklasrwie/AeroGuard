namespace AeroGuard.Api.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    UserDto User);

public sealed record UserDto(
    long Id,
    string Username,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    string Password,
    IReadOnlyList<string> Roles);

public sealed record ReplaceUserRolesRequest(IReadOnlyList<string> Roles);

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record ResetPasswordRequest(string Password);

public sealed record RoleDto(
    long Id,
    string Name,
    string Description,
    bool IsSystem,
    long UserCount,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateRoleRequest(string Name, string Description);

public sealed record AuditDto(
    long Id,
    long? ActorUserId,
    string? ActorUsername,
    string Action,
    string EntityType,
    string EntityId,
    object Details,
    DateTimeOffset OccurredAtUtc);
