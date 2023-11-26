using System.Buffers;
using System.Diagnostics;

namespace Prometheus;

/// <summary>
/// Represents a metric whose lifetime is managed by the caller, either via explicit leases or via extend-on-use behavior (implicit leases).
/// </summary>
/// <remarks>
/// Each metric handle maintains a reaper task that occasionally removes metrics that have expired. The reaper is started
/// when the first lifetime-managed metric is created and terminates when the last lifetime-managed metric expires.
/// This does mean that the metric handle may keep objects alive until expiration, even if the handle itself is no longer used.
/// TODO: Can we do something to reduce that risk?
/// </remarks>
internal abstract class ManagedLifetimeMetricHandle<TChild, TMetricInterface> : IManagedLifetimeMetricHandle<TMetricInterface>
    where TChild : ChildBase, TMetricInterface
    where TMetricInterface : ICollectorChild
{
    internal ManagedLifetimeMetricHandle(Collector<TChild> metric, TimeSpan expiresAfter)
    {
        _reaperFunc = Reaper;

        _metric = metric;
        _expiresAfter = expiresAfter;
    }

    protected readonly Collector<TChild> _metric;
    protected readonly TimeSpan _expiresAfter;

    public IDisposable AcquireLease(out TMetricInterface metric, params string[] labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        metric = child;

        return TakeLease(child);
    }

    public void WithLease(Action<TMetricInterface> action, params string[] labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        var lease = TakeRefLease(child);

        try
        {
            action(child);
        }
        finally
        {
            lease.Dispose();
        }
    }

    public async Task WithLeaseAsync(Func<TMetricInterface, Task> action, params string[] labelValues)
    {
        using var lease = AcquireLease(out var metric, labelValues);
        await action(metric);
    }

    public TResult WithLease<TResult>(Func<TMetricInterface, TResult> func, params string[] labelValues)
    {
        using var lease = AcquireLease(out var metric, labelValues);
        return func(metric);
    }

    public async Task<TResult> WithLeaseAsync<TResult>(Func<TMetricInterface, Task<TResult>> func, params string[] labelValues)
    {
        using var lease = AcquireLease(out var metric, labelValues);
        return await func(metric);
    }

    public abstract ICollector<TMetricInterface> WithExtendLifetimeOnUse();

    /// <summary>
    /// Internal to allow the delay logic to be replaced in test code, enabling (non-)expiration on demand.
    /// </summary>
    internal IDelayer Delayer = RealDelayer.Instance;

    #region Lease tracking
    // Contents modified via atomic operations, not guarded by locks.
    private sealed class LifetimeInfo
    {
        // Number of active leases. Nonzero value here indicates the lifetime extends forever.
        public int LeaseCount;

        // When the last lifetime related activity was performed. Expiration timer starts counting from here.
        // This is refreshed whenever a lease is released (a kept lease is a forever-keepalive, so we only care about releasing).
        public long KeepaliveTimestamp;

        // The lifetime has been ended, potentially while a lease was active. The next time a lease ends,
        // it will have to re-register the lifetime instead of just extending the existing one.
        public bool Ended;
    }

    private readonly Dictionary<TChild, LifetimeInfo> _lifetimes = new();

    // Guards the collection but not the contents.
    private readonly ReaderWriterLockSlim _lifetimesLock = new();

    private bool HasAnyTrackedLifetimes()
    {
        _lifetimesLock.EnterReadLock();

        try
        {
            return _lifetimes.Count != 0;
        }
        finally
        {
            _lifetimesLock.ExitReadLock();
        }
    }

    /// <summary>
    /// For testing only. Sets all keepalive timestamps to 0, which will cause all lifetimes to expire (if they have no leases).
    /// </summary>
    internal void ZeroAllKeepaliveTimestamps()
    {
        _lifetimesLock.EnterReadLock();

        try
        {
            foreach (var lifetime in _lifetimes.Values)
                Volatile.Write(ref lifetime.KeepaliveTimestamp, 0L);
        }
        finally
        {
            _lifetimesLock.ExitReadLock();
        }
    }

    private IDisposable TakeLease(TChild child)
    {
        var lifetime = GetOrCreateLifetimeAndIncrementLeaseCount(child);
        EnsureReaperActive();

        return new Lease(this, child, lifetime);
    }

    private RefLease TakeRefLease(TChild child)
    {
        var lifetime = GetOrCreateLifetimeAndIncrementLeaseCount(child);
        EnsureReaperActive();

        return new RefLease(this, child, lifetime);
    }

    private LifetimeInfo GetOrCreateLifetimeAndIncrementLeaseCount(TChild child)
    {
        _lifetimesLock.EnterReadLock();

        try
        {
            // Ideally, there already exists a registered lifetime for this metric instance.
            if (_lifetimes.TryGetValue(child, out var lifetime))
            {
                // Immediately increment it, to reduce the risk of any concurrent activities ending the lifetime.
                Interlocked.Increment(ref lifetime.LeaseCount);
                return lifetime;
            }
        }
        finally
        {
            _lifetimesLock.ExitReadLock();
        }

        // No lifetime registered yet - we need to take a write lock and register it.

        _lifetimesLock.EnterWriteLock();

        try
        {
            // Did we get lucky and someone already registered it?
            if (_lifetimes.TryGetValue(child, out var lifetime))
            {
                // Immediately increment it, to reduce the risk of any concurrent activities ending the lifetime.
                Interlocked.Increment(ref lifetime.LeaseCount);
                return lifetime;
            }

            // Did not get lucky. Make a new one.
            lifetime = new LifetimeInfo
            {
                LeaseCount = 1
            };

            _lifetimes.Add(child, lifetime);
            return lifetime;
        }
        finally
        {
            _lifetimesLock.ExitWriteLock();
        }
    }

    private void OnLeaseEnded(TChild child, LifetimeInfo lifetime)
    {
        // Update keepalive timestamp before anything else, to avoid racing.
        Volatile.Write(ref lifetime.KeepaliveTimestamp, LowGranularityTimeSource.GetStopwatchTimestamp());

        // If the lifetime has been ended while we still held a lease, it means there was a race that we lost.
        // The metric instance may or may not be still alive. To ensure proper cleanup, we re-register a lifetime
        // for the metric instance, which will ensure it gets cleaned up when it expires.
        if (Volatile.Read(ref lifetime.Ended))
        {
            // We just take a new lease and immediately dispose it. We are guaranteed not to loop here because the
            // reaper removes lifetimes from the dictionary once ended, so we can never run into the same lifetime again.
            TakeRefLease(child).Dispose();
        }

        // Finally, decrement the lease count to relinquish any claim on extending the lifetime.
        Interlocked.Decrement(ref lifetime.LeaseCount);
    }

    private sealed class Lease(ManagedLifetimeMetricHandle<TChild, TMetricInterface> parent, TChild child, LifetimeInfo lifetime) : IDisposable
    {
        public void Dispose() => parent.OnLeaseEnded(child, lifetime);
    }

    private readonly ref struct RefLease(ManagedLifetimeMetricHandle<TChild, TMetricInterface> parent, TChild child, LifetimeInfo lifetime)
    {
        public void Dispose() => parent.OnLeaseEnded(child, lifetime);
    }
    #endregion

    #region Reaper
    // Whether the reaper is currently active. This is set to true when a metric instance is created and
    // reset when the last metric instance expires (after which it may be set again).
    // We use atomic operations without locking.
    private int _reaperActiveBool = ReaperInactive;

    private const int ReaperActive = 1;
    private const int ReaperInactive = 0;

    /// <summary>
    /// Call this immediately after creating a metric instance that will eventually expire.
    /// </summary>
    private void EnsureReaperActive()
    {
        if (Interlocked.CompareExchange(ref _reaperActiveBool, ReaperActive, ReaperInactive) == ReaperActive)
        {
            // It was already active - nothing for us to do.
            return;
        }

        _ = Task.Run(_reaperFunc);
    }

    // Reimplementation of Stopwatch.GetElapsedTime (only available on .NET 7 or newer).
    private static TimeSpan GetElapsedTime(long start, long end)
        => new((long)((end - start) * ((double)10_000_000 / Stopwatch.Frequency)));

    private async Task Reaper()
    {
        while (true)
        {
            var now = LowGranularityTimeSource.GetStopwatchTimestamp();

            // Will contains the results of pass 1.
            TChild[] expiredInstancesBuffer = null!;
            int expiredInstanceCount = 0;

            // Pass 1: holding only a read lock, make a list of metric instances that have expired.
            _lifetimesLock.EnterReadLock();

            try
            {
                try
                {
                    expiredInstancesBuffer = ArrayPool<TChild>.Shared.Rent(_lifetimes.Count);

                    foreach (var pair in _lifetimes)
                    {
                        if (Volatile.Read(ref pair.Value.LeaseCount) != 0)
                            continue; // Not expired.

                        if (GetElapsedTime(Volatile.Read(ref pair.Value.KeepaliveTimestamp), now) < _expiresAfter)
                            continue; // Not expired.

                        // No leases and keepalive has expired - it is an expired instance!
                        expiredInstancesBuffer[expiredInstanceCount++] = pair.Key;
                    }
                }
                finally
                {
                    _lifetimesLock.ExitReadLock();
                }

                // Pass 2: if we have any work to do, take a write lock and remove the expired metric instances,
                // assuming our judgement about their expiration remains valid. We process and lock one by one,
                // to avoid holding locks for a long duration if many items expire at once - we are not in any rush.
                for (var i = 0; i < expiredInstanceCount; i++)
                {
                    var expiredInstance = expiredInstancesBuffer[i];

                    _lifetimesLock.EnterWriteLock();

                    try
                    {
                        if (!_lifetimes.TryGetValue(expiredInstance, out var lifetime))
                            continue; // Already gone, nothing for us to do.

                        // We need to check again whether the metric instance is still expired, because it may have been
                        // renewed by a new lease in the meantime. If it is still expired, we can remove it.
                        if (Volatile.Read(ref lifetime.LeaseCount) != 0)
                            continue; // Not expired.

                        if (GetElapsedTime(Volatile.Read(ref lifetime.KeepaliveTimestamp), now) < _expiresAfter)
                            continue; // Not expired.

                        // No leases and keepalive has expired - it is an expired instance!

                        // We mark the old lifetime as ended - if it happened that it got associated with a new lease
                        // (which is possible because we do not prevent lease-taking while in this loop), the new lease
                        // upon being ended will re-register the lifetime instead of just extending the existing one.
                        // We can be certain that any concurrent lifetime-affecting logic is using the same LifetimeInfo
                        // instance because the lifetime dictionary remains locked until we are done (by which time this flag is set).
                        Volatile.Write(ref lifetime.Ended, true);

                        _lifetimes.Remove(expiredInstance);

                        // If we did encounter a race, removing the metric instance here means that some metric value updates
                        // may go missing (until the next lease creates a new instance). This is acceptable behavior, to keep the code simple.
                        expiredInstance.Remove();
                    }
                    finally
                    {
                        _lifetimesLock.ExitWriteLock();
                    }
                }

                // Check if we need to shut down the reaper or keep going.
                _lifetimesLock.EnterReadLock();

                try
                {
                    if (_lifetimes.Count == 0)
                    {
                        CleanupReaper();
                        return;
                    }
                }
                finally
                {
                    _lifetimesLock.ExitReadLock();
                }

                // Work done! Go sleep a bit and come back when something may have expired.
                // We do not need to be too aggressive here, as expiration is not a hard schedule guarantee.
                await Delayer.Delay(_expiresAfter);
            }
            finally
            {
                ArrayPool<TChild>.Shared.Return(expiredInstancesBuffer);
            }
        }
    }

    /// <summary>
    /// Called when the reaper has noticed that all metric instances have expired and it has no more work to do. 
    /// </summary>
    private void CleanupReaper()
    {
        Volatile.Write(ref _reaperActiveBool, ReaperInactive);

        // The reaper is now gone. However, as we do not use locking here it is possible that someone already
        // added metric instances (which saw "oh reaper is still running") before we got here. Let's check - if
        // there appear to be metric instances registered, we may need to start the reaper again.
        if (HasAnyTrackedLifetimes())
            EnsureReaperActive();
    }

    private readonly Func<Task> _reaperFunc;
    #endregion
}