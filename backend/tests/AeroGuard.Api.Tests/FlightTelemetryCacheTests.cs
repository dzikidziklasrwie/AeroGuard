using System.Text.Json;
using AeroGuard.Api.Contracts;
using AeroGuard.Api.Operations;

namespace AeroGuard.Api.Tests;

public sealed class FlightTelemetryCacheTests
{
    private static readonly CancellationToken CancellationToken =
        TestContext.Current.CancellationToken;

    [Fact]
    public async Task ConcurrentCallersShareOneLoadAndReuseTheResult()
    {
        var cache = new FlightTelemetryCache(4);
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var telemetry = Telemetry("navigation");
        var loadCount = 0;

        async Task<IReadOnlyList<SubsystemTelemetryDto>> LoadAsync()
        {
            Interlocked.Increment(ref loadCount);
            loadStarted.TrySetResult();
            await releaseLoad.Task;
            return telemetry;
        }

        var first = cache.GetOrLoadAsync(7, 1000, LoadAsync, CancellationToken);
        await loadStarted.Task;
        var second = cache.GetOrLoadAsync(7, 1000, LoadAsync, CancellationToken);
        releaseLoad.SetResult();

        var loaded = await Task.WhenAll(first, second);
        var cached = await cache.GetOrLoadAsync(7, 1000, LoadAsync, CancellationToken);

        Assert.Equal(1, loadCount);
        Assert.All(loaded, result => Assert.Same(telemetry, result));
        Assert.Same(telemetry, cached);
    }

    [Fact]
    public async Task CancelledWaiterDoesNotCancelOrPoisonSharedLoad()
    {
        var cache = new FlightTelemetryCache(4);
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var telemetry = Telemetry("propulsion");
        var loadCount = 0;

        async Task<IReadOnlyList<SubsystemTelemetryDto>> LoadAsync()
        {
            Interlocked.Increment(ref loadCount);
            loadStarted.TrySetResult();
            await releaseLoad.Task;
            return telemetry;
        }

        using var cancellation = new CancellationTokenSource();
#pragma warning disable xUnit1051
        var cancelled = cache.GetOrLoadAsync(3, 0, LoadAsync, cancellation.Token);
#pragma warning restore xUnit1051
        await loadStarted.Task;
        var active = cache.GetOrLoadAsync(3, 0, LoadAsync, CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        releaseLoad.SetResult();

        Assert.Same(telemetry, await active);
        Assert.Same(
            telemetry,
            await cache.GetOrLoadAsync(3, 0, LoadAsync, CancellationToken));
        Assert.Equal(1, loadCount);
    }

    [Fact]
    public async Task LeastRecentlyUsedEntryIsEvictedAtCapacity()
    {
        var cache = new FlightTelemetryCache(2);
        var loadCount = 0;

        Task<IReadOnlyList<SubsystemTelemetryDto>> LoadAsync(string subsystem)
        {
            Interlocked.Increment(ref loadCount);
            return Task.FromResult(Telemetry(subsystem));
        }

        await cache.GetOrLoadAsync(1, 0, () => LoadAsync("one"), CancellationToken);
        await cache.GetOrLoadAsync(2, 0, () => LoadAsync("two"), CancellationToken);
        await cache.GetOrLoadAsync(1, 0, () => LoadAsync("one-reloaded"), CancellationToken);
        await cache.GetOrLoadAsync(3, 0, () => LoadAsync("three"), CancellationToken);
        var reloaded = await cache.GetOrLoadAsync(
            2,
            0,
            () => LoadAsync("two-reloaded"),
            CancellationToken);

        Assert.Equal(4, loadCount);
        Assert.Equal("two-reloaded", Assert.Single(reloaded).Subsystem);
    }

    private static IReadOnlyList<SubsystemTelemetryDto> Telemetry(string subsystem) =>
        [new SubsystemTelemetryDto(
            subsystem,
            "HEALTHY",
            true,
            "OK",
            JsonSerializer.SerializeToElement(new { value = 1 }))];
}
