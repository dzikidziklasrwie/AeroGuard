using System.Collections.Concurrent;
using AeroGuard.Api.Contracts;

namespace AeroGuard.Api.Operations;

public sealed class FlightTelemetryCache
{
    private const int DefaultCapacity = 4096;
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly Dictionary<FlightTelemetryCacheKey, LinkedListNode<CacheEntry>> _entries = [];
    private readonly LinkedList<CacheEntry> _leastRecentlyUsed = [];
    private readonly ConcurrentDictionary<
        FlightTelemetryCacheKey,
        Lazy<Task<IReadOnlyList<SubsystemTelemetryDto>>>> _loads = [];

    public FlightTelemetryCache()
        : this(DefaultCapacity)
    {
    }

    public FlightTelemetryCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public async Task<IReadOnlyList<SubsystemTelemetryDto>> GetOrLoadAsync(
        long runId,
        long timestampMs,
        Func<Task<IReadOnlyList<SubsystemTelemetryDto>>> loader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        var key = new FlightTelemetryCacheKey(runId, timestampMs);
        if (TryGet(key, out var cached))
        {
            return cached;
        }

        var load = _loads.GetOrAdd(
            key,
            cacheKey => new Lazy<Task<IReadOnlyList<SubsystemTelemetryDto>>>(
                () => LoadAndCacheAsync(cacheKey, loader),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return await load.Value.WaitAsync(cancellationToken);
    }

    private bool TryGet(
        FlightTelemetryCacheKey key,
        out IReadOnlyList<SubsystemTelemetryDto> telemetry)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var node))
            {
                telemetry = [];
                return false;
            }

            _leastRecentlyUsed.Remove(node);
            _leastRecentlyUsed.AddFirst(node);
            telemetry = node.Value.Telemetry;
            return true;
        }
    }

    private async Task<IReadOnlyList<SubsystemTelemetryDto>> LoadAndCacheAsync(
        FlightTelemetryCacheKey key,
        Func<Task<IReadOnlyList<SubsystemTelemetryDto>>> loader)
    {
        try
        {
            var telemetry = await loader();
            lock (_gate)
            {
                if (_entries.TryGetValue(key, out var existing))
                {
                    _leastRecentlyUsed.Remove(existing);
                }

                var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, telemetry));
                _leastRecentlyUsed.AddFirst(node);
                _entries[key] = node;
                while (_entries.Count > _capacity)
                {
                    var expired = _leastRecentlyUsed.Last!;
                    _leastRecentlyUsed.RemoveLast();
                    _entries.Remove(expired.Value.Key);
                }
            }

            return telemetry;
        }
        finally
        {
            _loads.TryRemove(key, out _);
        }
    }

    private readonly record struct FlightTelemetryCacheKey(long RunId, long TimestampMs);

    private sealed record CacheEntry(
        FlightTelemetryCacheKey Key,
        IReadOnlyList<SubsystemTelemetryDto> Telemetry);
}
