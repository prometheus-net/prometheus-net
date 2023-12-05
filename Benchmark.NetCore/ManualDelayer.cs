using Prometheus;

namespace Benchmark.NetCore;

/// <summary>
/// A delayer implementation that only returns from a delay when commanded to.
/// </summary>
internal sealed class ManualDelayer : IDelayer, IDisposable
{
    public void BreakAllDelays()
    {
        lock (_lock)
        {
            _tcs.TrySetResult();

            _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _delayTask = _tcs.Task;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            // If anything is still waiting, it shall not wait no more.
            _tcs.TrySetResult();

            // If anything will still wait in the future, it shall not wait at all.
            // Beware of creating spinning loops if you dispose the delayer when something still expects to be delayed.
            _delayTask = Task.CompletedTask;
        }
    }

    private readonly object _lock = new();
    private Task _delayTask;
    private TaskCompletionSource _tcs;

    public ManualDelayer()
    {
        _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _delayTask = _tcs.Task;
    }

    public Task Delay(TimeSpan duration)
    {
        lock (_lock)
            return _delayTask;
    }

    public Task Delay(TimeSpan duration, CancellationToken cancel)
    {
        lock (_lock)
            return _delayTask.WaitAsync(cancel);
    }
}
