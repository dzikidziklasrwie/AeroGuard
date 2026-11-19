using AeroGuard.Api.Contracts;
using AeroGuard.Api.Fleet;
using AeroGuard.Api.Identity;

namespace AeroGuard.Api.Endpoints;

public static class AircraftEndpoints
{
    public static IEndpointRouteBuilder MapAircraftEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/aircraft")
            .WithTags("Aircraft")
            .RequireAuthorization();

        group.MapGet("/", async (
            HttpContext context,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
            Results.Ok(await aircraft.GetVisibleAsync(
                CurrentUser.FromPrincipal(context.User),
                cancellationToken)))
            .WithName("GetAircraft")
            .WithSummary("List aircraft visible to the current account")
            .Produces<IReadOnlyList<AircraftDto>>();

        group.MapGet("/{id:long:min(1)}", async (
            long id,
            HttpContext context,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
            Results.Ok(await aircraft.GetVisibleAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                cancellationToken)))
            .WithName("GetAircraftById")
            .WithSummary("Get one visible aircraft")
            .Produces<AircraftDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            HttpContext context,
            CreateAircraftRequest request,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
        {
            var created = await aircraft.CreateAsync(
                CurrentUser.FromPrincipal(context.User),
                request,
                cancellationToken);
            return Results.Created($"/api/aircraft/{created.Id}", created);
        })
            .WithName("CreateAircraft")
            .WithSummary("Create an aircraft and assign its creator as owner")
            .Produces<AircraftDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:long:min(1)}", async (
            long id,
            HttpContext context,
            UpdateAircraftRequest request,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
            Results.Ok(await aircraft.UpdateAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                request,
                cancellationToken)))
            .WithName("UpdateAircraft")
            .WithSummary("Update aircraft data with an expected version")
            .Produces<AircraftDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:long:min(1)}", async (
            long id,
            HttpContext context,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
        {
            await aircraft.DeleteAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteAircraft")
            .WithSummary("Delete an aircraft as an administrator")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:long:min(1)}/assignments", async (
            long id,
            HttpContext context,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
            Results.Ok(await aircraft.GetAssignmentsAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                cancellationToken)))
            .WithName("GetAircraftAssignments")
            .WithSummary("List aircraft assignments as an administrator")
            .Produces<IReadOnlyList<AircraftAssignmentDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPut("/{id:long:min(1)}/assignments/{userId:long:min(1)}", async (
            long id,
            long userId,
            HttpContext context,
            SetAircraftAssignmentRequest request,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
            Results.Ok(await aircraft.SetAssignmentAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                userId,
                request,
                cancellationToken)))
            .WithName("SetAircraftAssignment")
            .WithSummary("Create or replace an aircraft assignment")
            .Produces<AircraftAssignmentDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:long:min(1)}/assignments/{userId:long:min(1)}", async (
            long id,
            long userId,
            HttpContext context,
            AircraftService aircraft,
            CancellationToken cancellationToken) =>
        {
            await aircraft.DeleteAssignmentAsync(
                CurrentUser.FromPrincipal(context.User),
                id,
                userId,
                cancellationToken);
            return Results.NoContent();
        })
            .WithName("DeleteAircraftAssignment")
            .WithSummary("Remove an aircraft assignment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
