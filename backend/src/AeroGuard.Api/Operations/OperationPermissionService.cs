using System.Globalization;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Errors;
using AeroGuard.Api.Fleet;
using AeroGuard.Api.Identity;

namespace AeroGuard.Api.Operations;

public sealed class OperationPermissionService(
    IOperationsRepository operationsRepository,
    IIdentityRepository identityRepository,
    IAuditRepository auditRepository,
    TimeProvider timeProvider)
{
    public async Task<OperationPermissionsDto> GetCurrentAsync(
        CurrentUser actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return new OperationPermissionsDto(
            await ResolveAsync(actor.Id, actor.Roles, cancellationToken));
    }

    public async Task<OperationPermissionsDto> GetForUserAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await identityRepository.GetUserAsync(userId, cancellationToken)
            ?? throw ApiProblemException.NotFound("The user does not exist.");
        return new OperationPermissionsDto(
            await ResolveAsync(user.Id, user.Roles, cancellationToken));
    }

    public async Task<OperationPermissionsDto> ReplaceAsync(
        CurrentUser actor,
        long userId,
        ReplaceOperationPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        var target = await identityRepository.GetUserAsync(userId, cancellationToken)
            ?? throw ApiProblemException.NotFound("The user does not exist.");
        var requested = (request.Permissions ?? [])
            .Select(permission => permission?.Trim().ToUpperInvariant() ?? string.Empty)
            .ToArray();
        var unknown = requested
            .Where(permission => !OperationPermissionNames.All.Contains(permission, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
        {
            throw ApiProblemException.Validation(
                "permissions",
                $"Unknown operation permission: {string.Join(", ", unknown)}.");
        }

        var enabled = requested.ToHashSet(StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow();
        await operationsRepository.ReplacePermissionOverridesAsync(
            userId,
            enabled,
            actor.Id,
            now,
            cancellationToken);
        await auditRepository.AppendAsync(
            actor.Id,
            "operations.permissions.replace",
            "user",
            userId.ToString(CultureInfo.InvariantCulture),
            new { target.Username, permissions = enabled.Order(StringComparer.Ordinal).ToArray() },
            now,
            cancellationToken);
        return new OperationPermissionsDto(
            await ResolveAsync(target.Id, target.Roles, cancellationToken));
    }

    public async Task<IReadOnlyList<string>> ResolveAsync(
        long userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase))
        {
            return OperationPermissionNames.All;
        }

        var overrides = await operationsRepository.GetPermissionOverridesAsync(
            userId,
            cancellationToken);
        if (overrides.Count > 0)
        {
            return OperationPermissionNames.All
                .Where(permission => overrides.GetValueOrDefault(permission))
                .ToArray();
        }

        var defaults = new HashSet<string>(StringComparer.Ordinal);
        var isOperator = roles.Contains(BuiltInRoles.Operator, StringComparer.OrdinalIgnoreCase);
        var isViewer = roles.Contains(BuiltInRoles.Viewer, StringComparer.OrdinalIgnoreCase);
        if (isOperator || isViewer)
        {
            defaults.Add(OperationPermissionNames.ChatSend);
        }

        if (isOperator)
        {
            defaults.Add(OperationPermissionNames.FlightPlaceOwn);
            defaults.Add(OperationPermissionNames.FlightRemoveOwn);
            defaults.Add(OperationPermissionNames.FlightReconfigureOwn);
            defaults.Add(OperationPermissionNames.TelemetryShare);
        }

        return OperationPermissionNames.All
            .Where(defaults.Contains)
            .ToArray();
    }
}
