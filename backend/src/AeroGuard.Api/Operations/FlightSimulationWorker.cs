namespace AeroGuard.Api.Operations;

public sealed class FlightSimulationWorker(
    OperationsBroadcaster broadcaster,
    OperationsBroadcastRefreshSignal refreshSignal)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await refreshSignal.WaitAsync(TimeSpan.FromSeconds(1), stoppingToken);
            await broadcaster.BroadcastAllAsync(stoppingToken);
        }
    }
}
