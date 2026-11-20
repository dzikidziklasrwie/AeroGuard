using AeroGuard.Api.Contracts;
using AeroGuard.Api.Identity;
using AeroGuard.Api.Operations;

namespace AeroGuard.Api.Endpoints;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/operations")
            .WithTags("Operations")
            .RequireAuthorization();

        group.MapGet("/permissions", async (
            HttpContext context,
            OperationPermissionService permissions,
            CancellationToken cancellationToken) =>
            Results.Ok(await permissions.GetCurrentAsync(
                CurrentUser.FromPrincipal(context.User),
                cancellationToken)))
            .WithName("GetCurrentOperationPermissions")
            .WithSummary("List effective map and chat permissions for the current account")
            .Produces<OperationPermissionsDto>();

        group.MapGet("/flights", async (
            HttpContext context,
            OperationsService operations,
            CancellationToken cancellationToken) =>
            Results.Ok(await operations.GetVisibleFlightsAsync(
                CurrentUser.FromPrincipal(context.User),
                cancellationToken)))
            .WithName("GetActiveFlights")
            .WithSummary("List active map flights visible to the current account")
            .Produces<IReadOnlyList<FlightDto>>();

        group.MapPost("/flights", async (
            HttpContext context,
            PlaceFlightRequest request,
            OperationsService operations,
            CancellationToken cancellationToken) =>
        {
            var created = await operations.PlaceFlightAsync(
                CurrentUser.FromPrincipal(context.User),
                request,
                cancellationToken);
            return Results.Created($"/api/operations/flights/{created.Id}", created);
        })
            .WithName("PlaceFlight")
            .WithSummary("Place an owned aircraft on the operations map")
            .Produces<FlightDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("operations-write");

        group.MapPut("/flights/{id:long:min(1)}", async (
            long id,
            HttpContext context,
            UpdateFlightRequest request,
            OperationsService operations,
            CancellationToken cancellationToken) =>
            Results.Ok(await operations.UpdateFlightAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                request,
                cancellationToken)))
            .WithName("UpdateFlight")
            .WithSummary("Change the route or telemetry scenario for an active flight")
            .Produces<FlightDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting("operations-write");

        group.MapDelete("/flights/{id:long:min(1)}", async (
            long id,
            HttpContext context,
            OperationsService operations,
            CancellationToken cancellationToken) =>
        {
            await operations.RemoveFlightAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                cancellationToken);
            return Results.NoContent();
        })
            .WithName("RemoveFlight")
            .WithSummary("Remove an authorized aircraft from the operations map")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting("operations-write");

        group.MapGet("/flights/{id:long:min(1)}/messages", async (
            long id,
            int? limit,
            HttpContext context,
            OperationsService operations,
            CancellationToken cancellationToken) =>
            Results.Ok(await operations.GetMessagesAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                limit ?? 100,
                cancellationToken)))
            .WithName("GetFlightMessages")
            .WithSummary("List recent chat messages for a visible flight")
            .Produces<IReadOnlyList<FlightMessageDto>>();

        group.MapPost("/flights/{id:long:min(1)}/messages", async (
            long id,
            HttpContext context,
            SendFlightMessageRequest request,
            OperationsService operations,
            CancellationToken cancellationToken) =>
        {
            var created = await operations.SendMessageAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                request,
                cancellationToken);
            return Results.Created(
                $"/api/operations/flights/{id}/messages/{created.Id}",
                created);
        })
            .WithName("SendFlightMessage")
            .WithSummary("Send text, live position, or subsystem telemetry to flight chat")
            .Produces<FlightMessageDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireRateLimiting("operations-chat");

        return endpoints;
    }
}
