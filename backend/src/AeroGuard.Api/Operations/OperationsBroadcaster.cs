using AeroGuard.Api.Contracts;
using AeroGuard.Api.Identity;
using Microsoft.AspNetCore.SignalR;

namespace AeroGuard.Api.Operations;

public sealed class OperationsBroadcaster(
    IHubContext<OperationsHub> hub,
    IOperationsRepository operationsRepository,
    IIdentityRepository identityRepository,
    OperationsConnectionRegistry connections,
    OperationPermissionService permissionService,
    FlightSnapshotFactory snapshotFactory,
    ILogger<OperationsBroadcaster> logger)
{
    private long _eventSequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task BroadcastAllAsync(CancellationToken cancellationToken = default)
    {
        var flights = await operationsRepository.GetAllFlightsAsync(cancellationToken);
        foreach (var flight in flights)
        {
            try
            {
                await BroadcastFlightAsync(flight, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Live update failed for flight {FlightId}",
                    flight.Id);
            }
        }
    }

    public async Task BroadcastFlightAsync(
        FlightRecord flight,
        CancellationToken cancellationToken = default)
    {
        var eventSequence = NextEventSequence();
        var adminSnapshot = await snapshotFactory.CreateAsync(
            flight,
            OperationPermissionNames.All,
            true,
            cancellationToken) with
        {
            EventSequence = eventSequence,
        };
        var administrators = await GetAdministratorIdsAsync(cancellationToken);
        var administratorConnections = await connections.GetActiveConnectionIdsAsync(
            administrators,
            cancellationToken);
        await hub.Clients
            .Clients(administratorConnections)
            .SendAsync("flight_updated", adminSnapshot, cancellationToken);

        var userIds = await operationsRepository.GetAssignedUserIdsAsync(
            flight.Id,
            cancellationToken);
        foreach (var userId in userIds)
        {
            var user = await identityRepository.GetUserAsync(userId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                continue;
            }

            if (user.Roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var visibleFlight = await operationsRepository.GetFlightAsync(
                flight.Id,
                userId,
                false,
                cancellationToken);
            if (visibleFlight is null)
            {
                continue;
            }

            var permissions = await permissionService.ResolveAsync(
                userId,
                user.Roles,
                cancellationToken);
            var snapshot = await snapshotFactory.CreateAsync(
                visibleFlight,
                permissions,
                false,
                cancellationToken) with
            {
                EventSequence = eventSequence,
            };
            var userConnections = await connections.GetActiveConnectionIdsAsync(
                [userId],
                cancellationToken);
            await hub.Clients
                .Clients(userConnections)
                .SendAsync("flight_updated", snapshot, cancellationToken);
        }
    }

    public async Task BroadcastRemovedAsync(
        long flightId,
        IReadOnlyList<long> assignedUserIds,
        CancellationToken cancellationToken = default)
    {
        var payload = new { id = flightId, event_sequence = NextEventSequence() };
        var recipients = (await GetAdministratorIdsAsync(cancellationToken))
            .Concat(assignedUserIds)
            .Distinct()
            .ToArray();
        var recipientConnections = await connections.GetActiveConnectionIdsAsync(
            recipients,
            cancellationToken);
        await hub.Clients
            .Clients(recipientConnections)
            .SendAsync("flight_removed", payload, cancellationToken);
    }

    public async Task BroadcastMessageAsync(
        FlightMessageDto message,
        IReadOnlyList<long> assignedUserIds,
        CancellationToken cancellationToken = default)
    {
        var recipients = (await GetAdministratorIdsAsync(cancellationToken))
            .Concat(assignedUserIds)
            .Distinct()
            .ToArray();
        var recipientConnections = await connections.GetActiveConnectionIdsAsync(
            recipients,
            cancellationToken);
        await hub.Clients
            .Clients(recipientConnections)
            .SendAsync("message_posted", message, cancellationToken);
    }

    public async Task BroadcastVisibilityRemovedAsync(
        long flightId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var recipientConnections = await connections.GetActiveConnectionIdsAsync(
            [userId],
            cancellationToken);
        await hub.Clients
            .Clients(recipientConnections)
            .SendAsync(
                "flight_removed",
                new { id = flightId, event_sequence = NextEventSequence() },
                cancellationToken);
    }

    private long NextEventSequence()
    {
        while (true)
        {
            var current = Volatile.Read(ref _eventSequence);
            var next = Math.Max(current + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (Interlocked.CompareExchange(ref _eventSequence, next, current) == current)
            {
                return next;
            }
        }
    }

    private async Task<IReadOnlyList<long>> GetAdministratorIdsAsync(
        CancellationToken cancellationToken) =>
        (await identityRepository.GetUsersAsync(cancellationToken))
        .Where(user => user.IsActive
            && user.Roles.Contains(BuiltInRoles.Admin, StringComparer.OrdinalIgnoreCase))
        .Select(user => user.Id)
        .ToArray();
}
