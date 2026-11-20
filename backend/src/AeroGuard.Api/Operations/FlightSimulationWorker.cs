namespace AeroGuard.Api.Operations;

public sealed class FlightSimulationWorker(
    OperationsBroadcaster broadcaster)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await broadcaster.BroadcastAllAsync(stoppingToken);
        }
    }
}
