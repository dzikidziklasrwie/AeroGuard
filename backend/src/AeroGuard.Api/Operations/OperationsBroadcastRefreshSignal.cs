namespace AeroGuard.Api.Operations;

public sealed class OperationsBroadcastRefreshSignal : IDisposable
{
    private readonly SemaphoreSlim _pending = new(0, 1);

    public void RequestRefresh()
    {
        lock (_pending)
        {
            if (_pending.CurrentCount == 0)
            {
                _pending.Release();
            }
        }
    }

    public async Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken)
        => await _pending.WaitAsync(interval, cancellationToken);

    public void Dispose() => _pending.Dispose();
}
