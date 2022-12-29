using System.Collections.Concurrent;

namespace Prometheus;

internal abstract class ManagedLifetimeMetricHandle<TChild, TMetricInterface> : IManagedLifetimeMetricHandle<TMetricInterface>
    where TChild : ChildBase, TMetricInterface
    where TMetricInterface : ICollectorChild
{
    internal ManagedLifetimeMetricHandle(Collector<TChild> metric, TimeSpan expiresAfter)
    {
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
        var lease = TakeLeaseFast(child);

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

    /// <summary>
    /// An instance of LifetimeManager takes care of the lifetime of a single child metric:
    /// * It maintains the count of active leases.
    /// * It schedules removal for a suitable moment after the last lease is released.
    /// 
    /// Once the lifetime manager decides to remove the metric, it can no longer be used and a new lifetime manager must be allocated.
    /// Taking new leases after removal will have no effect without recycling the lifetime manager (because it will be a lease on
    /// a metric instance that has already been removed from its parent metric family - even if you update the value, it is no longer exported).
    /// </summary>
    /// <remarks>
    /// Expiration is managed on a loosely accurate method - when the first lease is taken, an expiration timer is started.
    /// This timer will tick at a regular interval and, upon each tick, check whether the metric needs to expire. That's it.
    /// The metric expiration is guaranteed to be no less than [expiresAfter] has elapsed, but may be more as the timer ticks on its own clock.
    /// </remarks>
    private sealed class LifetimeManager
    {
        public LifetimeManager(TChild child, TimeSpan expiresAfter, IDelayer delayer, Action<TChild> remove)
        {
            _child = child;
            _expiresAfter = expiresAfter;
            _delayer = delayer;
            _remove = remove;

            // NB! There may be optimistic copies made by the ConcurrentDictionary - this may be such a copy!
            _reusableLease = new ReusableLease(ReleaseLease);
        }

        private readonly TChild _child;
        private readonly TimeSpan _expiresAfter;
        private readonly IDelayer _delayer;
        private readonly Action<TChild> _remove;

        private readonly object _lock = new();
        private int _leaseCount = 0;

        // Taking or releasing a lease will always start a new epoch. The expiration timer simply checks whether the epoch changes between two ticks.
        // If the epoch changes, it must mean there was some lease-related activity and it will do nothing. If the epoch remains the same and the lease
        // count is 0, the metric has expired and will be removed.
        private int _epoch = 0;

        // We start the expiration timer the first time a lease is taken.
        private bool _timerStarted;

        private readonly ReusableLease _reusableLease;

        public IDisposable TakeLease()
        {
            TakeLeaseCore();

            return new Lease(ReleaseLease);
        }

        /// <summary>
        /// Returns a reusable lease-releaser object. Only for internal use - to avoid allocating on every lease.
        /// </summary>
        internal IDisposable TakeLeaseFast()
        {
            TakeLeaseCore();

            return _reusableLease;
        }

        private void TakeLeaseCore()
        {
            lock (_lock)
            {
                EnsureExpirationTimerStarted();

                _leaseCount++;
                unchecked { _epoch++; }
            }
        }

        private void ReleaseLease()
        {
            lock (_lock)
            {
                _leaseCount--;
                unchecked { _epoch++; }
            }
        }

        private void EnsureExpirationTimerStarted()
        {
            if (_timerStarted)
                return;

            _timerStarted = true;

            _ = Task.Run(ExecuteExpirationTimer);
        }

        private async Task ExecuteExpirationTimer()
        {
            while (true)
            {
                int epochBeforeDelay;
                
                lock (_lock)
                    epochBeforeDelay = _epoch;

                // We iterate on the expiration interval. This means that the real lifetime of a metric may be up to 2x the expiration interval.
                // This is fine - we are intentionally loose here, to avoid the timer logic being scheduled too aggressively. Approximate is good enough.
                await _delayer.Delay(_expiresAfter);

                lock (_lock)
                {
                    if (_leaseCount != 0)
                        continue; // Will not expire if there are active leases.

                    if (_epoch != epochBeforeDelay)
                        continue; // Will not expire if some leasing activity happened during this interval.
                }

                // Expired!
                //
                // It is possible that a new lease still gets taken before this call completes, because we are not yet holding the lifetime manager write lock that
                // guards against new leases being taken. In that case, the new lease will be a dud - it will fail to extend the lifetime because the removal happens
                // already now, even if the new lease is taken. This is intentional, to keep the code simple.
                _remove(_child);
                break;
            }
        }

        private sealed class Lease : IDisposable
        {
            public Lease(Action releaseLease)
            {
                _releaseLease = releaseLease;
            }

            ~Lease()
            {
                // Anomalous but we'll do the best we can.
                Dispose();
            }

            private readonly Action _releaseLease;

            private bool _disposed;
            private readonly object _lock = new();

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposed)
                        return;

                    _disposed = true;
                }

                _releaseLease();
                GC.SuppressFinalize(this);
            }
        }

        public sealed class ReusableLease : IDisposable
        {
            public ReusableLease(Action releaseLease)
            {
                _releaseLease = releaseLease;
            }

            private readonly Action _releaseLease;

            public void Dispose()
            {
                _releaseLease();
            }
        }
    }

    /// <summary>
    /// The lifetime manager of each child is stored here. We optimistically allocate them to avoid synchronization on the hot path.
    /// We only synchronize when disposing of children whose lifetime has expired, to avoid racing between concurrent removal and re-publishing.
    /// 
    /// Avoiding races during lifetime manager allocation:
    /// * Creating a new instance of LifetimeManager is harmless in duplicate.
    ///     - An instance of LifetimeManager will only "start" once its methods are called, not in its ctor.
    ///     - ConcurrentDictionary will throw away an optimistically created duplicate.
    /// * Creating a new instance takes a reader lock to allow allocation to be blocked by removal logic.
    /// * Removal will take a writer lock to prevent concurrent allocataions (which also implies preventing concurrent new leases that might "renew" a lifetime).
    ///     - It can be that between "deletion needed" event and write lock being taken, the state of the lifetime manager changes because of
    ///       actions done by holders of the read lock (e.g. new lease added). For code simplicity, we accept this as a gap where we may lose data (such a lease fails to renew/start a lifetime).
    /// </summary>
    private readonly ConcurrentDictionary<TChild, LifetimeManager> _lifetimeManagers = new();

    private readonly ReaderWriterLockSlim _lifetimeManagersLock = new();

    /// <summary>
    /// Takes a new lease on a child, allocating a new lifetime manager if necessary.
    /// Any number of leases may be held concurrently on the same child.
    /// As soon as the last lease is released, the child is eligible for removal, though new leases may still be taken to extend the lifetime.
    /// </summary>
    private IDisposable TakeLease(TChild child)
    {
        // We synchronize here to ensure that we do not get a LifetimeManager that has already ended the lifetime.
        _lifetimeManagersLock.EnterReadLock();

        try
        {
            return GetOrAddLifetimeManagerCore(child).TakeLease();
        }
        finally
        {
            _lifetimeManagersLock.ExitReadLock();
        }
    }

    // Non-allocating variant, for internal use via WithLease().
    private IDisposable TakeLeaseFast(TChild child)
    {
        // We synchronize here to ensure that we do not get a LifetimeManager that has already ended the lifetime.
        _lifetimeManagersLock.EnterReadLock();

        try
        {
            return GetOrAddLifetimeManagerCore(child).TakeLeaseFast();
        }
        finally
        {
            _lifetimeManagersLock.ExitReadLock();
        }
    }

    private LifetimeManager GetOrAddLifetimeManagerCore(TChild child)
    {
        // Let's assume optimistically that in the typical case, there already is a lifetime manager for it.
        if (_lifetimeManagers.TryGetValue(child, out var existing))
            return existing;

        return _lifetimeManagers.GetOrAdd(child, CreateLifetimeManager);
    }

    private LifetimeManager CreateLifetimeManager(TChild child)
    {
        return new LifetimeManager(child, _expiresAfter, Delayer, DeleteMetricOuter);
    }

    /// <summary>
    /// Performs the locking necessary to ensure that a LifetimeManager that ends the lifetime does not get reused.
    /// </summary>
    private void DeleteMetricOuter(TChild child)
    {
        _lifetimeManagersLock.EnterWriteLock();

        try
        {
            // We assume here that LifetimeManagers are not so buggy to call this method twice (when another LifetimeManager has replaced the old one).
            _ = _lifetimeManagers.TryRemove(child, out _);
            child.Remove();
        }
        finally
        {
            _lifetimeManagersLock.ExitWriteLock();
        }
    }
}