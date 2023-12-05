using System.Diagnostics;

namespace Prometheus;

/// <summary>
/// An implementation that uses Task.Delay(), for use at runtime.
/// </summary>
internal sealed class RealDelayer : IDelayer
{
    public static readonly RealDelayer Instance = new();

    [DebuggerStepThrough]
    public Task Delay(TimeSpan duration) => Task.Delay(duration);

    [DebuggerStepThrough]
    public Task Delay(TimeSpan duration, CancellationToken cancel) => Task.Delay(duration, cancel);
}
