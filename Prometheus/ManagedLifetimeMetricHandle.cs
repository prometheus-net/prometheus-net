using System.Buffers;

namespace Prometheus;

/// <summary>
/// Represents a metric whose lifetime is managed by the caller, either via explicit leases or via extend-on-use behavior (implicit leases).
/// </summary>
/// <remarks>
/// Each metric handle maintains a reaper task that occasionally removes metrics that have expired. The reaper is started
/// when the first lifetime-managed metric is created and terminates when the last lifetime-managed metric expires.
/// This does mean that the metric handle may keep objects alive until expiration, even if the handle itself is no longer used.
/// </remarks>
internal abstract class ManagedLifetimeMetricHandle<TChild, TMetricInterface>
    : IManagedLifetimeMetricHandle<TMetricInterface>, INotifyLeaseEnded
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

    #region Lease(string[])
    public IDisposable AcquireLease(out TMetricInterface metric, params string[] labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        metric = child;

        return TakeLease(child);
    }

    public RefLease AcquireRefLease(out TMetricInterface metric, params string[] labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        metric = child;

        return TakeRefLease(child);
    }

    public void WithLease(Action<TMetricInterface> action, params string[] labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        using var lease = TakeRefLease(child);

        action(child);
    }

    public void WithLease<TArg>(Action<TArg, TMetricInterface> action, TArg arg, params string[] labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        using var lease = TakeRefLease(child);

        action(arg, child);
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
    #endregion

    #region Lease(ReadOnlyMemory<string>)
    public IDisposable AcquireLease(out TMetricInterface metric, ReadOnlyMemory<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        metric = child;

        return TakeLease(child);
    }

    public RefLease AcquireRefLease(out TMetricInterface metric, ReadOnlyMemory<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        metric = child;

        return TakeRefLease(child);
    }

    public void WithLease(Action<TMetricInterface> action, ReadOnlyMemory<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        using var lease = TakeRefLease(child);

        action(child);
    }

    public void WithLease<TArg>(Action<TArg, TMetricInterface> action, TArg arg, ReadOnlyMemory<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        using var lease = TakeRefLease(child);

        action(arg, child);
    }

    public async Task WithLeaseAsync(Func<TMetricInterface, Task> action, ReadOnlyMemory<string> labelValues)
    {
        using var lease = AcquireLease(out var metric, labelValues);
        await action(metric);
    }

    public TResult WithLease<TResult>(Func<TMetricInterface, TResult> func, ReadOnlyMemory<string> labelValues)
    {
        using var lease = AcquireLease(out var metric, labelValues);
        return func(metric);
    }

    public async Task<TResult> WithLeaseAsync<TResult>(Func<TMetricInterface, Task<TResult>> func, ReadOnlyMemory<string> labelValues)
    {
        using var lease = AcquireLease(out var metric, labelValues);
        return await func(metric);
    }
    #endregion

    #region Lease(ReadOnlySpan<string>)
    public IDisposable AcquireLease(out TMetricInterface metric, ReadOnlySpan<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        metric = child;

        return TakeLease(child);
    }

    public RefLease AcquireRefLease(out TMetricInterface metric, ReadOnlySpan<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        metric = child;

        return TakeRefLease(child);
    }

    public void WithLease(Action<TMetricInterface> action, ReadOnlySpan<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        using var lease = TakeRefLease(child);

        action(child);
    }

    public void WithLease<TArg>(Action<TArg, TMetricInterface> action, TArg arg, ReadOnlySpan<string> labelValues)
    {
        var child = _metric.WithLabels(labelValues);
        using var lease = TakeRefLease(child);

        action(arg, child);
    }

    public TResult WithLease<TResult>(Func<TMetricInterface, TResult> func, ReadOnlySpan<string> labelValues)
    {
        using var lease = AcquireLease(out var metric, labelValues);
        return func(metric);
    }
    #endregion

    public abstract ICollector<TMetricInterface> WithExtendLifetimeOnUse();

    /// <summary>
    /// Internal to allow the delay logic to be replaced in test code, enabling (non-)expiration on demand.
    /// </summary>
    internal IDelayer Delayer = RealDelayer.Instance;

    #region Lease tracking
    private readonly Dictionary<TChild, ChildLifetimeInfo> _lifetimes = new();

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
    /// For testing only. Sets all keepalive timestamps to a time in the disstant past,
    /// which will cause all lifetimes to expire (if they have no leases).
    /// </summary>
    internal void SetAllKeepaliveTimestampsToDistantPast()
    {
        // We cannot just zero this because zero is the machine start timestamp, so zero is not necessarily
        // far in the past (especially if the machine is a build agent that just started up). 1 year negative should work, though.
        var distantPast = -PlatformCompatibilityHelpers.ElapsedToTimeStopwatchTicks(TimeSpan.FromDays(365));

        _lifetimesLock.EnterReadLock();

        try
        {
            foreach (var lifetime in _lifetimes.Values)
                Volatile.Write(ref lifetime.KeepaliveTimestamp, distantPast);
        }
        finally
        {
            _lifetimesLock.ExitReadLock();
        }
    }

    /// <summary>
    /// For anomaly analysis during testing only.
    /// </summary>
    internal void DebugDumpLifetimes()
    {
        _lifetimesLock.EnterReadLock();

        try
        {
            Console.WriteLine($"Dumping {_lifetimes.Count} lifetimes of {_metric}. Reaper status: {Volatile.Read(ref _reaperActiveBool)}.");

            foreach (var pair in _lifetimes)
            {
                Console.WriteLine($"{pair.Key} -> {pair.Value}");
            }
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

    private ChildLifetimeInfo GetOrCreateLifetimeAndIncrementLeaseCount(TChild child)
    {
        _lifetimesLock.EnterReadLock();

        try
        {
            // Ideally, there already exists a registered lifetime for this metric instance.
            if (_lifetimes.TryGetValue(child, out var existing))
            {
                // Immediately increment it, to reduce the risk of any concurrent activities ending the lifetime.
                Interlocked.Increment(ref existing.LeaseCount);
                return existing;
            }
        }
        finally
        {
            _lifetimesLock.ExitReadLock();
        }

        // No lifetime registered yet - we need to take a write lock and register it.
        var newLifetime = new ChildLifetimeInfo
        {
            LeaseCount = 1
        };

        _lifetimesLock.EnterWriteLock();

        try
        {
#if NET
            // It could be that someone beats us to it! Probably not, though.
            if (_lifetimes.TryAdd(child, newLifetime))
                return newLifetime;

            var existing = _lifetimes[child];

            // Immediately increment it, to reduce the risk of any concurrent activities ending the lifetime.
            // Even if something does, it is not the end of the world - the reaper will create a new lifetime when it realizes this happened.
            Interlocked.Increment(ref existing.LeaseCount);
            return existing;
#else
            // On .NET Fx we need to do the pessimistic case first because there is no TryAdd().
            if (_lifetimes.TryGetValue(child, out var existing))
            {
                // Immediately increment it, to reduce the risk of any concurrent activities ending the lifetime.
                // Even if something does, it is not the end of the world - the reaper will create a new lifetime when it realizes this happened.
                Interlocked.Increment(ref existing.LeaseCount);
                return existing;
            }

            _lifetimes.Add(child, newLifetime);
            return newLifetime;
#endif
        }
        finally
        {
            _lifetimesLock.ExitWriteLock();
        }
    }

    internal void OnLeaseEnded(TChild child, ChildLifetimeInfo lifetime)
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

    void INotifyLeaseEnded.OnLeaseEnded(object child, ChildLifetimeInfo lifetime)
    {
        OnLeaseEnded((TChild)child, lifetime);
    }

    private sealed class Lease(ManagedLifetimeMetricHandle<TChild, TMetricInterface> parent, TChild child, ChildLifetimeInfo lifetime) : IDisposable
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

                        if (PlatformCompatibilityHelpers.StopwatchGetElapsedTime(Volatile.Read(ref pair.Value.KeepaliveTimestamp), now) < _expiresAfter)
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

                        if (PlatformCompatibilityHelpers.StopwatchGetElapsedTime(Volatile.Read(ref lifetime.KeepaliveTimestamp), now) < _expiresAfter)
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
            }
            finally
            {
                ArrayPool<TChild>.Shared.Return(expiredInstancesBuffer);
            }

            // Check if we need to shut down the reaper or keep going.
            _lifetimesLock.EnterReadLock();

            try
            {
                if (_lifetimes.Count != 0)
                    goto has_more_work;
            }
            finally
            {
                _lifetimesLock.ExitReadLock();
            }

            CleanupReaper();
            return;

        has_more_work:
            // Work done! Go sleep a bit and come back when something may have expired.
            // We do not need to be too aggressive here, as expiration is not a hard schedule guarantee.
            await Delayer.Delay(_expiresAfter);
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