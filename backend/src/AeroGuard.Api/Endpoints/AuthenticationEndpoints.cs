using AeroGuard.Api.Contracts;
using AeroGuard.Api.Errors;
using AeroGuard.Api.Identity;

namespace AeroGuard.Api.Endpoints;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Access");

        group.MapPost("/login", async (
            LoginRequest request,
            AuthenticationService authentication,
            CancellationToken cancellationToken) =>
            Results.Ok(await authentication.LoginAsync(request, cancellationToken)))
            .AllowAnonymous()
            .RequireRateLimiting("login")
            .WithName("Login")
            .WithSummary("Create a revocable bearer session")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", async (
            HttpContext context,
            IIdentityRepository repository,
            CancellationToken cancellationToken) =>
        {
            var current = CurrentUser.FromPrincipal(context.User);
            var user = await repository.GetUserAsync(current.Id, cancellationToken)
                ?? throw ApiProblemException.NotFound("The user does not exist.");
            return Results.Ok(user.ToDto());
        })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the authenticated account")
            .Produces<UserDto>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (
            HttpContext context,
            AuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            var current = CurrentUser.FromPrincipal(context.User);
            await authentication.LogoutAsync(
                current.SessionId,
                current.Id,
                cancellationToken);
            return Results.NoContent();
        })
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Revoke the current bearer session")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
