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
        if (!connections.HasConnections)
        {
            return;
        }

        var administrators = await GetAdministratorIdsAsync(cancellationToken);
        var flights = await operationsRepository.GetAllFlightsAsync(cancellationToken);
        foreach (var flight in flights)
        {
            try
            {
                await BroadcastFlightCoreAsync(
                    flight,
                    administrators,
                    cancellationToken);
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
        if (!connections.HasConnections)
        {
            return;
        }

        var administrators = await GetAdministratorIdsAsync(cancellationToken);
        await BroadcastFlightCoreAsync(flight, administrators, cancellationToken);
    }

    private async Task BroadcastFlightCoreAsync(
        FlightRecord flight,
        IReadOnlyList<long> administrators,
        CancellationToken cancellationToken)
    {
        var administratorIds = administrators.ToHashSet();
        var assignedUserIds = await operationsRepository.GetAssignedUserIdsAsync(
            flight.Id,
            cancellationToken);
        var recipientIds = administrators
            .Concat(assignedUserIds)
            .Distinct()
            .ToArray();
        var activeConnections = await connections.GetActiveConnectionsAsync(
            recipientIds,
            cancellationToken);
        if (activeConnections.Count == 0)
        {
            return;
        }

        var connectionIdsByUser = activeConnections
            .GroupBy(connection => connection.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(connection => connection.ConnectionId).ToArray());
        var prepared = await snapshotFactory.PrepareAsync(flight, cancellationToken);
        var eventSequence = NextEventSequence();

        var administratorConnections = activeConnections
            .Where(connection => administratorIds.Contains(connection.UserId))
            .Select(connection => connection.ConnectionId)
            .ToArray();
        if (administratorConnections.Length > 0)
        {
            var adminSnapshot = FlightSnapshotFactory.Create(
                flight,
                prepared,
                OperationPermissionNames.All,
                true,
                eventSequence);
            await hub.Clients
                .Clients(administratorConnections)
                .SendAsync("flight_updated", adminSnapshot, cancellationToken);
        }

        foreach (var userId in assignedUserIds.Distinct())
        {
            if (administratorIds.Contains(userId)
                || !connectionIdsByUser.TryGetValue(userId, out var userConnections))
            {
                continue;
            }

            var user = await identityRepository.GetUserAsync(userId, cancellationToken);
            if (user is null || !user.IsActive)
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
            var snapshot = FlightSnapshotFactory.Create(
                visibleFlight,
                prepared,
                permissions,
                false,
                eventSequence);
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
        if (!connections.HasConnections)
        {
            return;
        }

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
        if (!connections.HasConnections)
        {
            return;
        }

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
        if (!connections.HasConnections)
        {
            return;
        }

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
        await identityRepository.GetActiveUserIdsInRoleAsync(
            BuiltInRoles.Admin,
            cancellationToken);
}
