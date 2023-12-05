namespace Prometheus;

/// <summary>
/// Abstraction over Task.Delay() to allow custom delay logic to be injected in tests.
/// </summary>
internal interface IDelayer
{
    Task Delay(TimeSpan duration);
    Task Delay(TimeSpan duration, CancellationToken cancel);
}
