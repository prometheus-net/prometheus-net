namespace Prometheus;

/// <summary>
/// A stack-only struct for holding a lease on a lifetime-managed metric.
/// Helps avoid allocation when you need to take a lease in a synchronous context where stack-only structs are allowed.
/// </summary>
public readonly ref struct RefLease
{
    internal RefLease(INotifyLeaseEnded notifyLeaseEnded, object child, ChildLifetimeInfo lifetime)
    {
        _notifyLeaseEnded = notifyLeaseEnded;
        _child = child;
        _lifetime = lifetime;
    }

    private readonly INotifyLeaseEnded _notifyLeaseEnded;
    private readonly object _child;
    private readonly ChildLifetimeInfo _lifetime;

    public void Dispose() => _notifyLeaseEnded.OnLeaseEnded(_child, _lifetime);
}