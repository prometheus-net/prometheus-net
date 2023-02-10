namespace Prometheus;

internal sealed class NoopDisposable : IDisposable
{
    public void Dispose()
    {
    }
}
