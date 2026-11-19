using AeroGuard.Api.Contracts;
using AeroGuard.Api.Identity;

namespace AeroGuard.Api.Endpoints;

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin")
            .WithTags("Administration")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/users", async (
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
            Results.Ok(await accounts.GetUsersAsync(cancellationToken)))
            .WithName("GetUsers")
            .WithSummary("List user accounts")
            .Produces<IReadOnlyList<UserDto>>();

        group.MapPost("/users", async (
            HttpContext context,
            CreateUserRequest request,
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
        {
            var user = await accounts.CreateUserAsync(
                CurrentUser.FromPrincipal(context.User),
                request,
                cancellationToken);
            return Results.Created($"/api/admin/users/{user.Id}", user);
        })
            .WithName("CreateUser")
            .WithSummary("Create a user account")
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/users/{id:long:min(1)}/roles", async (
            long id,
            HttpContext context,
            ReplaceUserRolesRequest request,
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
            Results.Ok(await accounts.ReplaceRolesAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                request,
                cancellationToken)))
            .WithName("ReplaceUserRoles")
            .WithSummary("Replace all roles assigned to a user")
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/users/{id:long:min(1)}/status", async (
            long id,
            HttpContext context,
            UpdateUserStatusRequest request,
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
            Results.Ok(await accounts.SetStatusAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                request,
                cancellationToken)))
            .WithName("UpdateUserStatus")
            .WithSummary("Activate or deactivate a user")
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/users/{id:long:min(1)}/password", async (
            long id,
            HttpContext context,
            ResetPasswordRequest request,
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
        {
            await accounts.ResetPasswordAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                request,
                cancellationToken);
            return Results.NoContent();
        })
            .WithName("ResetUserPassword")
            .WithSummary("Set a new password and revoke the user's sessions")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/roles", async (
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
            Results.Ok(await accounts.GetRolesAsync(cancellationToken)))
            .WithName("GetRoles")
            .WithSummary("List roles and assignment counts")
            .Produces<IReadOnlyList<RoleDto>>();

        group.MapPost("/roles", async (
            HttpContext context,
            CreateRoleRequest request,
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
        {
            var role = await accounts.CreateRoleAsync(
                CurrentUser.FromPrincipal(context.User),
                request,
                cancellationToken);
            return Results.Created($"/api/admin/roles/{role.Id}", role);
        })
            .WithName("CreateRole")
            .WithSummary("Create a custom role")
            .Produces<RoleDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/roles/{id:long:min(1)}", async (
            long id,
            HttpContext context,
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
        {
            await accounts.DeleteRoleAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteRole")
            .WithSummary("Delete an unused custom role")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/audit", async (
            int? limit,
            AccountAdministrationService accounts,
            CancellationToken cancellationToken) =>
            Results.Ok(await accounts.GetAuditAsync(limit ?? 50, cancellationToken)))
            .WithName("GetAuditLog")
            .WithSummary("List recent account and aircraft changes")
            .Produces<IReadOnlyList<AuditDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
