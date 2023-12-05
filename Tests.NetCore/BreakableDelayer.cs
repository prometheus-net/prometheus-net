using System;
using System.Threading;
using System.Threading.Tasks;

namespace Prometheus.Tests;

/// <summary>
/// A delayer that seems to work as usual except it can be instructed to end all waits immediately.
/// </summary>
/// <remarks>
/// Thread-safe.
/// </remarks>
public sealed class BreakableDelayer : IDelayer
{
    /// <summary>
    /// Ends all delays and pretends the timers all elapsed.
    /// </summary>
    public void BreakAllDelays()
    {
        CancellationTokenSource old;

        lock (_lock)
        {
            // Have to replace CTS first to ensure that any new calls get new CTS.
            // Very important because canceling the CTS actually executes code until the next await.
            old = _cts;
            _cts = new CancellationTokenSource();
        }

        old.Cancel();
        old.Dispose();
    }

    private CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly object _lock = new object();

    public async Task Delay(TimeSpan duration)
    {
        CancellationToken cancel;

        lock (_lock)
            cancel = _cts.Token;

        try
        {
            await Task.Delay(duration, cancel);
        }
        catch (TaskCanceledException)
        {
        }
    }

    public async Task Delay(TimeSpan duration, CancellationToken requestedCancel)
    {
        CancellationTokenSource callCts;
        CancellationToken cancel;

        lock (_lock)
        {
            callCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, requestedCancel);
            cancel = callCts.Token;
        }

        try
        {
            await Task.Delay(duration, cancel);
        }
        catch (TaskCanceledException)
        {
            requestedCancel.ThrowIfCancellationRequested();
        }
        finally
        {
            callCts.Dispose();
        }
    }
}
