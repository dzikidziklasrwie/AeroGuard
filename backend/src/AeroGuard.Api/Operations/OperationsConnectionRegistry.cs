using System.Collections.Concurrent;
using AeroGuard.Api.Identity;

namespace AeroGuard.Api.Operations;

public sealed record OperationsConnection(
    string ConnectionId,
    long UserId,
    long SessionId);

public sealed class OperationsConnectionRegistry(
    IIdentityRepository identityRepository,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, OperationsConnection> _connections =
        new(StringComparer.Ordinal);

    public bool HasConnections => !_connections.IsEmpty;

    public IReadOnlyList<long> GetConnectedUserIds() =>
        _connections.Values
            .Select(connection => connection.UserId)
            .Distinct()
            .ToArray();

    public void Register(string connectionId, long userId, long sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        _connections[connectionId] = new OperationsConnection(connectionId, userId, sessionId);
    }

    public void Remove(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        _connections.TryRemove(connectionId, out _);
    }

    public async Task<IReadOnlyList<string>> GetActiveConnectionIdsAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken = default) =>
        (await GetActiveConnectionsAsync(userIds, cancellationToken))
        .Select(connection => connection.ConnectionId)
        .ToArray();

    public async Task<IReadOnlyList<OperationsConnection>> GetActiveConnectionsAsync(
        IReadOnlyCollection<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var allowedUsers = userIds.ToHashSet();
        var active = new List<OperationsConnection>();
        var sessionStates = new Dictionary<long, bool>();
        var now = timeProvider.GetUtcNow();
        foreach (var connection in _connections.Values)
        {
            if (!allowedUsers.Contains(connection.UserId))
            {
                continue;
            }

            if (!sessionStates.TryGetValue(connection.SessionId, out var isActive))
            {
                isActive = await identityRepository.IsSessionActiveAsync(
                    connection.SessionId,
                    now,
                    cancellationToken);
                sessionStates[connection.SessionId] = isActive;
            }

            if (isActive)
            {
                active.Add(connection);
            }
            else
            {
                _connections.TryRemove(connection.ConnectionId, out _);
            }
        }

        return active;
    }
}
