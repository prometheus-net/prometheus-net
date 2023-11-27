namespace Prometheus;

/// <summary>
/// Describes a lifetime of a lifetime-managed metric instance.
/// </summary>
/// <remarks>
/// Contents modified via atomic operations, not guarded by locks.
/// </remarks>
internal sealed class ChildLifetimeInfo
{
    /// <summary>
    /// Number of active leases. Nonzero value here indicates the lifetime extends forever.
    /// </summary>
    public int LeaseCount;

    /// <summary>
    /// When the last lifetime related activity was performed. Expiration timer starts counting from here.
    /// This is refreshed whenever a lease is released (a kept lease is a forever-keepalive, so we only care about releasing).
    /// </summary>
    public long KeepaliveTimestamp;

    /// <summary>
    /// The lifetime has been ended, potentially while a lease was active. The next time a lease ends,
    /// it will have to re-register the lifetime instead of just extending the existing one.
    /// </summary>
    public bool Ended;
}