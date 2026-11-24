using System.Collections.Concurrent;
using System.Reflection;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Data;
using AeroGuard.Api.Fleet;
using AeroGuard.Api.Identity;
using AeroGuard.Api.Operations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace AeroGuard.Api.Tests;

public sealed class OperationsBroadcasterTests
{
    private static readonly CancellationToken CancellationToken =
        TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset Now =
        new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PeriodicCycleLoadsRecipientOnceAndSkipsMixedFlightVersions()
    {
        var globalFlights = new[]
        {
            Flight(1, 4, "global-one"),
            Flight(2, 7, "global-two"),
            Flight(3, 9, "global-three"),
        };
        var visibleFlights = new[]
        {
            globalFlights[0] with { CurrentUserAccess = AircraftAccessLevels.Owner, Scenario = "stale-one" },
            globalFlights[1] with { CurrentUserAccess = AircraftAccessLevels.Maintainer, Scenario = "stale-two" },
            globalFlights[2] with { CurrentUserAccess = AircraftAccessLevels.Owner, Version = 8 },
        };
        var getAllFlightsCount = 0;
        var getVisibleFlightsCount = 0;
        var getPermissionOverridesCount = 0;
        var getUserCount = 0;
        var sessionValidationCount = 0;
        var administratorLookupCount = 0;
        var operations = CreateProxy<IOperationsRepository>((method, _) => method.Name switch
        {
            nameof(IOperationsRepository.GetAllFlightsAsync) => Counted(
                ref getAllFlightsCount,
                Task.FromResult<IReadOnlyList<FlightRecord>>(globalFlights)),
            nameof(IOperationsRepository.GetVisibleFlightsAsync) => Counted(
                ref getVisibleFlightsCount,
                Task.FromResult<IReadOnlyList<FlightRecord>>(visibleFlights)),
            nameof(IOperationsRepository.GetPermissionOverridesAsync) => Counted(
                ref getPermissionOverridesCount,
                Task.FromResult<IReadOnlyDictionary<string, bool>>(
                    new Dictionary<string, bool>(StringComparer.Ordinal))),
            _ => throw new InvalidOperationException($"Unexpected operations call: {method.Name}"),
        });
        var user = new UserAccount(
            7,
            "operator",
            "Operator",
            "unused",
            true,
            Now,
            Now,
            [BuiltInRoles.Operator]);
        var identity = CreateProxy<IIdentityRepository>((method, _) => method.Name switch
        {
            nameof(IIdentityRepository.GetUserAsync) => Counted(
                ref getUserCount,
                Task.FromResult<UserAccount?>(user)),
            nameof(IIdentityRepository.IsSessionActiveAsync) => Counted(
                ref sessionValidationCount,
                Task.FromResult(true)),
            nameof(IIdentityRepository.GetActiveUserIdsInRoleAsync) => Counted(
                ref administratorLookupCount,
                Task.FromResult<IReadOnlyList<long>>([])),
            _ => throw new InvalidOperationException($"Unexpected identity call: {method.Name}"),
        });
        var audit = CreateProxy<IAuditRepository>((method, _) =>
            throw new InvalidOperationException($"Unexpected audit call: {method.Name}"));
        var connectionFactory = CreateProxy<ISqliteConnectionFactory>((method, _) =>
            throw new InvalidOperationException($"Unexpected archive call: {method.Name}"));
        var cache = new FlightTelemetryCache(4);
        await cache.GetOrLoadAsync(
            100,
            0,
            () => Task.FromResult<IReadOnlyList<SubsystemTelemetryDto>>([]),
            CancellationToken);
        var timeProvider = new FixedTimeProvider(Now);
        var snapshotFactory = new FlightSnapshotFactory(
            new ArchiveRepository(connectionFactory),
            cache,
            timeProvider);
        var registry = new OperationsConnectionRegistry(identity, timeProvider);
        registry.Register("connection-7", user.Id, 700);
        var hub = new RecordingHubContext();
        var broadcaster = new OperationsBroadcaster(
            hub,
            operations,
            identity,
            registry,
            new OperationPermissionService(operations, identity, audit, timeProvider),
            snapshotFactory,
            NullLogger<OperationsBroadcaster>.Instance);

        await broadcaster.BroadcastAllAsync(CancellationToken);

        Assert.Equal(1, getAllFlightsCount);
        Assert.Equal(1, getVisibleFlightsCount);
        Assert.Equal(1, getPermissionOverridesCount);
        Assert.Equal(1, getUserCount);
        Assert.Equal(1, sessionValidationCount);
        Assert.Equal(1, administratorLookupCount);
        var snapshots = hub.Messages
            .Select(message => Assert.IsType<FlightDto>(Assert.Single(message.Arguments)))
            .OrderBy(snapshot => snapshot.Id)
            .ToArray();
        Assert.Equal(2, snapshots.Length);
        Assert.Equal("global-one", snapshots[0].Scenario);
        Assert.Equal(AircraftAccessLevels.Owner, snapshots[0].CurrentUserAccess);
        Assert.Equal("global-two", snapshots[1].Scenario);
        Assert.Equal(AircraftAccessLevels.Maintainer, snapshots[1].CurrentUserAccess);
    }

    private static T Counted<T>(ref int counter, T result)
    {
        counter++;
        return result;
    }

    private static FlightRecord Flight(long id, long version, string scenario) =>
        new(
            id,
            id,
            $"AG-{id:000}",
            $"Aircraft {id}",
            "ACTIVE",
            11,
            "owner",
            "Owner",
            null,
            100,
            scenario,
            scenario,
            45_000,
            100,
            FlightScopeTypes.World,
            "WORLD",
            52.0,
            21.0,
            780.0,
            id,
            version,
            11,
            Now,
            Now);

    private static T CreateProxy<T>(
        Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, TestDispatchProxy>();
        ((TestDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private class TestDispatchProxy : DispatchProxy
    {
        public required Func<MethodInfo, object?[]?, object?> Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(
                targetMethod ?? throw new InvalidOperationException("The proxy method is missing."),
                args);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingHubContext : IHubContext<OperationsHub>
    {
        private readonly RecordingClientProxy _proxy = new();

        public IHubClients Clients => new RecordingHubClients(_proxy);

        public IGroupManager Groups { get; } = new NoOpGroupManager();

        public IReadOnlyList<RecordedMessage> Messages => _proxy.Messages.ToArray();
    }

    private sealed class RecordingHubClients(IClientProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;

        public IClientProxy Client(string connectionId) => proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;

        public IClientProxy Group(string groupName) => proxy;

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) => proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;

        public IClientProxy User(string userId) => proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public ConcurrentQueue<RecordedMessage> Messages { get; } = [];

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(new RecordedMessage(method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record RecordedMessage(string Method, object?[] Arguments);
}
