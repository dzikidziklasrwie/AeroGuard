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
        var connectedUserIds = connections.GetConnectedUserIds();
        if (connectedUserIds.Count == 0)
        {
            return;
        }

        var activeConnections = await connections.GetActiveConnectionsAsync(
            connectedUserIds,
            cancellationToken);
        if (activeConnections.Count == 0)
        {
            return;
        }

        var eventSequence = NextEventSequence();
        var administratorsTask = GetAdministratorIdsAsync(cancellationToken);
        var flightsTask = operationsRepository.GetAllFlightsAsync(cancellationToken);
        await Task.WhenAll(administratorsTask, flightsTask);
        var administratorIds = (await administratorsTask).ToHashSet();
        var flights = await flightsTask;
        var connectionsByUser = activeConnections
            .GroupBy(connection => connection.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(connection => connection.ConnectionId).ToArray());
        var administratorConnections = activeConnections
            .Where(connection => administratorIds.Contains(connection.UserId))
            .Select(connection => connection.ConnectionId)
            .ToArray();
        var recipientLoads = connectionsByUser
            .Where(entry => !administratorIds.Contains(entry.Key))
            .Select(entry => LoadRecipientAsync(
                entry.Key,
                entry.Value,
                cancellationToken));
        var recipients = (await Task.WhenAll(recipientLoads))
            .OfType<BroadcastRecipient>()
            .ToArray();
        var recipientsByFlight = new Dictionary<long, List<BroadcastFlightRecipient>>();
        foreach (var recipient in recipients)
        {
            foreach (var visibleFlight in recipient.VisibleFlights)
            {
                if (!recipientsByFlight.TryGetValue(visibleFlight.Id, out var flightRecipients))
                {
                    flightRecipients = [];
                    recipientsByFlight[visibleFlight.Id] = flightRecipients;
                }

                flightRecipients.Add(new BroadcastFlightRecipient(
                    recipient.ConnectionIds,
                    recipient.Permissions,
                    visibleFlight));
            }
        }

        foreach (var flight in flights)
        {
            recipientsByFlight.TryGetValue(flight.Id, out var flightRecipients);
            var currentRecipients = flightRecipients?
                .Where(recipient => recipient.VisibleFlight.Version == flight.Version)
                .ToArray()
                ?? [];
            if (administratorConnections.Length == 0 && currentRecipients.Length == 0)
            {
                continue;
            }

            try
            {
                await BroadcastPreparedFlightAsync(
                    flight,
                    administratorConnections,
                    currentRecipients,
                    eventSequence,
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

    private async Task<BroadcastRecipient?> LoadRecipientAsync(
        long userId,
        IReadOnlyList<string> connectionIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await identityRepository.GetUserAsync(userId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            var visibleFlightsTask = operationsRepository.GetVisibleFlightsAsync(
                userId,
                false,
                cancellationToken);
            var permissionsTask = permissionService.ResolveAsync(
                userId,
                user.Roles,
                cancellationToken);
            await Task.WhenAll(visibleFlightsTask, permissionsTask);
            return new BroadcastRecipient(
                connectionIds,
                await permissionsTask,
                await visibleFlightsTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Live update recipient preparation failed for user {UserId}",
                userId);
            return null;
        }
    }

    private async Task BroadcastPreparedFlightAsync(
        FlightRecord flight,
        IReadOnlyList<string> administratorConnections,
        IReadOnlyList<BroadcastFlightRecipient> recipients,
        long eventSequence,
        CancellationToken cancellationToken)
    {
        var prepared = await snapshotFactory.PrepareAsync(flight, cancellationToken);
        if (administratorConnections.Count > 0)
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

        foreach (var recipient in recipients)
        {
            var visibleFlight = flight with
            {
                CurrentUserAccess = recipient.VisibleFlight.CurrentUserAccess,
            };
            var snapshot = FlightSnapshotFactory.Create(
                visibleFlight,
                prepared,
                recipient.Permissions,
                false,
                eventSequence);
            await hub.Clients
                .Clients(recipient.ConnectionIds)
                .SendAsync("flight_updated", snapshot, cancellationToken);
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

        var eventSequence = NextEventSequence();
        var administrators = await GetAdministratorIdsAsync(cancellationToken);
        await BroadcastFlightCoreAsync(
            flight,
            administrators,
            eventSequence,
            cancellationToken);
    }

    private async Task BroadcastFlightCoreAsync(
        FlightRecord flight,
        IReadOnlyList<long> administrators,
        long eventSequence,
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

        var eventSequence = NextEventSequence();
        var payload = new { id = flightId, event_sequence = eventSequence };
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

        var eventSequence = NextEventSequence();
        var recipientConnections = await connections.GetActiveConnectionIdsAsync(
            [userId],
            cancellationToken);
        await hub.Clients
            .Clients(recipientConnections)
            .SendAsync(
                "flight_removed",
                new { id = flightId, event_sequence = eventSequence },
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

    private sealed record BroadcastRecipient(
        IReadOnlyList<string> ConnectionIds,
        IReadOnlyList<string> Permissions,
        IReadOnlyList<FlightRecord> VisibleFlights);

    private sealed record BroadcastFlightRecipient(
        IReadOnlyList<string> ConnectionIds,
        IReadOnlyList<string> Permissions,
        FlightRecord VisibleFlight);
}
