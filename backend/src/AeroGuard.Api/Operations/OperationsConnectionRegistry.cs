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
        CancellationToken cancellationToken = default)
    {
        var allowedUsers = userIds.ToHashSet();
        var active = new List<string>();
        foreach (var connection in _connections.Values)
        {
            if (!allowedUsers.Contains(connection.UserId))
            {
                continue;
            }

            if (await identityRepository.IsSessionActiveAsync(
                    connection.SessionId,
                    timeProvider.GetUtcNow(),
                    cancellationToken))
            {
                active.Add(connection.ConnectionId);
            }
            else
            {
                _connections.TryRemove(connection.ConnectionId, out _);
            }
        }

        return active;
    }
}
