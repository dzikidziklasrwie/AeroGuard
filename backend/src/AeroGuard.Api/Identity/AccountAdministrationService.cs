using System.Globalization;
using System.Text.Json;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Errors;
using AeroGuard.Api.Fleet;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Identity;

public sealed class AccountAdministrationService(
    IIdentityRepository repository,
    IPasswordHasher<UserAccount> passwordHasher,
    IAuditRepository auditRepository,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default) =>
        (await repository.GetUsersAsync(cancellationToken)).Select(DtoMappings.ToDto).ToArray();

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(
        CancellationToken cancellationToken = default) =>
        (await repository.GetRolesAsync(cancellationToken)).Select(DtoMappings.ToDto).ToArray();

    public async Task<UserDto> CreateUserAsync(
        CurrentUser actor,
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        var username = CredentialRules.ValidateUsername(request.Username);
        var displayName = CredentialRules.ValidateDisplayName(request.DisplayName);
        var password = CredentialRules.ValidatePassword(request.Password);
        var roles = await ValidateRolesAsync(request.Roles, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var draft = new UserAccount(
            0,
            username,
            displayName,
            string.Empty,
            true,
            now,
            now,
            roles);
        var passwordHash = passwordHasher.HashPassword(draft, password);

        UserAccount user;
        try
        {
            user = await repository.CreateUserAsync(
                username,
                displayName,
                passwordHash,
                roles,
                now,
                cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw ApiProblemException.Conflict("A user with that username already exists.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "user.create",
            "user",
            user.Id.ToString(CultureInfo.InvariantCulture),
            new { user.Username, user.DisplayName, user.Roles },
            now,
            cancellationToken);
        return user.ToDto();
    }

    public async Task<UserDto> ReplaceRolesAsync(
        CurrentUser actor,
        long userId,
        ReplaceUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);

        var current = await RequireUserAsync(userId, cancellationToken);
        var roles = await ValidateRolesAsync(request.Roles, cancellationToken);
        if (current.IsActive
            && current.Roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase)
            && !roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase)
            && await repository.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            throw ApiProblemException.Conflict("The last active administrator must keep the Admin role.");
        }

        var now = timeProvider.GetUtcNow();
        var updated = await repository.ReplaceRolesAsync(userId, roles, now, cancellationToken)
            ?? throw ApiProblemException.NotFound("The user does not exist.");
        await auditRepository.AppendAsync(
            actor.Id,
            "user.roles.replace",
            "user",
            userId.ToString(CultureInfo.InvariantCulture),
            new { before = current.Roles, after = updated.Roles },
            now,
            cancellationToken);
        return updated.ToDto();
    }

    public async Task<UserDto> SetStatusAsync(
        CurrentUser actor,
        long userId,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var current = await RequireUserAsync(userId, cancellationToken);
        if (!request.IsActive && actor.Id == userId)
        {
            throw ApiProblemException.Conflict("An administrator cannot deactivate the current account.");
        }

        if (!request.IsActive
            && current.IsActive
            && current.Roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase)
            && await repository.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            throw ApiProblemException.Conflict("The last active administrator cannot be deactivated.");
        }

        var now = timeProvider.GetUtcNow();
        var updated = await repository.SetUserStatusAsync(userId, request.IsActive, now, cancellationToken)
            ?? throw ApiProblemException.NotFound("The user does not exist.");
        await auditRepository.AppendAsync(
            actor.Id,
            "user.status.update",
            "user",
            userId.ToString(CultureInfo.InvariantCulture),
            new { before = current.IsActive, after = updated.IsActive },
            now,
            cancellationToken);
        return updated.ToDto();
    }

    public async Task ResetPasswordAsync(
        CurrentUser actor,
        long userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var user = await RequireUserAsync(userId, cancellationToken);
        var password = CredentialRules.ValidatePassword(request.Password);
        var hash = passwordHasher.HashPassword(user, password);
        var now = timeProvider.GetUtcNow();
        if (!await repository.SetPasswordHashAsync(userId, hash, now, cancellationToken))
        {
            throw ApiProblemException.NotFound("The user does not exist.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "user.password.reset",
            "user",
            userId.ToString(CultureInfo.InvariantCulture),
            new { sessions_revoked = true },
            now,
            cancellationToken);
    }

    public async Task<RoleDto> CreateRoleAsync(
        CurrentUser actor,
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var name = CredentialRules.ValidateRoleName(request.Name);
        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length > 200)
        {
            throw ApiProblemException.Validation("description", "Use no more than 200 characters.");
        }

        var now = timeProvider.GetUtcNow();
        RoleRecord role;
        try
        {
            role = await repository.CreateRoleAsync(name, description, now, cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw ApiProblemException.Conflict("A role with that name already exists.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "role.create",
            "role",
            role.Id.ToString(CultureInfo.InvariantCulture),
            new { role.Name, role.Description },
            now,
            cancellationToken);
        return role.ToDto();
    }

    public async Task DeleteRoleAsync(
        CurrentUser actor,
        long roleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var role = (await repository.GetRolesAsync(cancellationToken))
            .SingleOrDefault(candidate => candidate.Id == roleId)
            ?? throw ApiProblemException.NotFound("The role does not exist.");
        if (role.IsSystem)
        {
            throw ApiProblemException.Conflict("Built-in roles cannot be deleted.");
        }

        if (role.UserCount > 0)
        {
            throw ApiProblemException.Conflict("Remove the role from every user before deleting it.");
        }

        try
        {
            if (!await repository.DeleteRoleAsync(roleId, cancellationToken))
            {
                throw ApiProblemException.NotFound("The role does not exist.");
            }
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw ApiProblemException.Conflict("The role is still assigned to a user.");
        }

        await auditRepository.AppendAsync(
            actor.Id,
            "role.delete",
            "role",
            roleId.ToString(CultureInfo.InvariantCulture),
            new { role.Name },
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<AuditDto>> GetAuditAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 200)
        {
            throw ApiProblemException.Validation("limit", "Use a value from 1 to 200.");
        }

        var records = await auditRepository.GetRecentAsync(limit, cancellationToken);
        return records.Select(record =>
        {
            using var document = JsonDocument.Parse(record.DetailsJson);
            return new AuditDto(
                record.Id,
                record.ActorUserId,
                record.ActorUsername,
                record.Action,
                record.EntityType,
                record.EntityId,
                document.RootElement.Clone(),
                record.OccurredAtUtc);
        }).ToArray();
    }

    private async Task<UserAccount> RequireUserAsync(
        long userId,
        CancellationToken cancellationToken) =>
        await repository.GetUserAsync(userId, cancellationToken)
        ?? throw ApiProblemException.NotFound("The user does not exist.");

    private async Task<IReadOnlyList<string>> ValidateRolesAsync(
        IReadOnlyList<string>? requestedRoles,
        CancellationToken cancellationToken)
    {
        if (requestedRoles is null || requestedRoles.Count is < 1 or > 10)
        {
            throw ApiProblemException.Validation("roles", "Assign from 1 to 10 roles.");
        }

        var available = (await repository.GetRolesAsync(cancellationToken))
            .ToDictionary(role => role.Name, StringComparer.OrdinalIgnoreCase);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var requestedRole in requestedRoles)
        {
            var name = requestedRole?.Trim() ?? string.Empty;
            if (!available.TryGetValue(name, out var role))
            {
                throw ApiProblemException.Validation("roles", $"Role '{name}' does not exist.");
            }

            result.Add(role.Name);
        }

        return result.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
