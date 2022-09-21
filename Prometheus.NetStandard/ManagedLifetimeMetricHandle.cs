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
        var labels = new Labels(_metric.LabelNames, labelValues);
        var lease = TakeLease(labels);
        metric = _metric.WithLabels(labelValues);
        return lease;
    }

    public abstract ICollector<TMetricInterface> WithExtendLifetimeOnUse();

    /// <summary>
    /// Internal to allow the delay logic to be replaced in test code, enabling (non-)expiration on demand.
    /// </summary>
    internal IDelayer Delayer = RealDelayer.Instance;

    /// <summary>
    /// An instance of LifetimeManager takes care of the lifetime of a single child metric:
    /// * It maintains the count of active leases.
    /// * It schedules unpublishing after the last lease is released (and cancels unpublishing if a new lease is taken).
    /// 
    /// Once the lifetime manager decides to unpublish the metric, it can no longer be used and a new lifetime manager must be allocated.
    /// Taking new leases after unpublishing will have no effect without recycling the lifetime manager.
    /// </summary>
    private sealed class LifetimeManager
    {
        public LifetimeManager(Labels labels, TimeSpan expiresAfter, IDelayer delayer, Action<Labels> unpublish)
        {
            _labels = labels;
            _expiresAfter = expiresAfter;
            _delayer = delayer;
            _unpublish = unpublish;

            // NB! There may be optimistic copies made by the ConcurrentDictionary - this may be such a copy!
        }

        private readonly Labels _labels;
        private readonly TimeSpan _expiresAfter;
        private readonly IDelayer _delayer;
        private readonly Action<Labels> _unpublish;

        private readonly object _lock = new();
        private int _leaseCount = 0;
        // Used to cancel a scheduled unpublishing task when a stale LifetimeManager becomes active again.
        private CancellationTokenSource? _cts;
        // We need to ensure only-once unpublishing semantics, as we are racy by design.
        private bool _unpublished;

        public IDisposable TakeLease()
        {
            lock (_lock)
            {
                if (_leaseCount == 0)
                {
                    // There may have been an existing scheduled unpublishing. Cancel it.
                    _cts?.Cancel(); // Or this may be the first lease ever, in which case there is no CTS yet.
                    _cts?.Dispose();
                    _cts = new();
                }

                _leaseCount++;
            }

            return new Lease(ReleaseLease);
        }

        private void ReleaseLease()
        {
            lock (_lock)
            {
                _leaseCount--;

                if (_leaseCount == 0)
                {
                    if (_unpublished)
                        return;

                    var cancel = _cts!.Token;

                    // We want to avoid throwing exceptions here, so let's try be smart.
                    _ = _delayer.Delay(_expiresAfter, cancel)
                        .ContinueWith(task =>
                        {
                            // Unpublish after the timer expires.
                            lock (_lock)
                            {
                                _unpublished = true;
                            }

                            _unpublish(_labels);

                            // Only execute the above block if we did not get canceled.
                        }, TaskContinuationOptions.NotOnCanceled);
                }
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
    }

    /// <summary>
    /// The lifetime manager of each child is stored here. We optimistically allocate them to avoid synchronization on the hot path.
    /// We only synchronize when disposing of children whose lifetime has expired, to avoid racing between concurrent unpublishing and re-publishing.
    /// 
    /// Avoiding races during lifetime manager allocation:
    /// * Creating a new instance of LifetimeManager is harmless in duplicate.
    ///     - An instance of LifetimeManager will only "start" once its methods are called, not in its ctor.
    ///     - ConcurrentDictionary will throw away an optimistically created duplicate.
    /// * Creating a new instance takes a reader lock to allow allocation to be blocked by unpublishing logic.
    /// * Unpublishing will take a writer lock to prevent concurrent allocataions (which also implies preventing concurrent new leases that might "renew" a lifetime).
    ///     - It can be that between "unpublishing needed" event and write lock being taken, the state of the lifetime manager changes because of
    ///       actions done by holders of the read lock (e.g. new lease added). For code simplicity, we accept this as a gap where we may lose data (such a lease fails to renew/start a lifetime).
    /// </summary>
    private readonly ConcurrentDictionary<Labels, LifetimeManager> _lifetimeManagers = new();

    private readonly ReaderWriterLockSlim _lifetimeManagersLock = new();

    /// <summary>
    /// Takes a new lease of a child with a specific set of labels, allocating a new lifetime manager if necessary.
    /// Any number of leases may be held concurrently for the same child.
    /// As soon as the last lease is released, the child is eligible for unpublishing, though new leases may still be taken to extend the lifetime.
    /// </summary>
    /// <param name="labels">Labels that uniquely identify this child.</param>
    private IDisposable TakeLease(Labels labels)
    {
        // We synchronize here to ensure that we do not get a LifetimeManager that has already ended the lifetime.
        _lifetimeManagersLock.EnterReadLock();

        try
        {
            var lifetimeManager = _lifetimeManagers.GetOrAdd(labels, CreateLifetimeManager);
            return lifetimeManager.TakeLease();
        }
        finally
        {
            _lifetimeManagersLock.ExitReadLock();
        }
    }

    private LifetimeManager CreateLifetimeManager(Labels labels)
    {
        return new LifetimeManager(labels, _expiresAfter, Delayer, UnpublishOuter);
    }

    /// <summary>
    /// Performs the locking necessary to ensure that a LifetimeManager that ends the lifetime does not get reused.
    /// </summary>
    private void UnpublishOuter(Labels labels)
    {
        _lifetimeManagersLock.EnterWriteLock();

        try
        {
            // We assume here that LifetimeManagers are not so buggy to call this method twice (when another LifetimeManager has replaced the old one).
            _ = _lifetimeManagers.TryRemove(labels, out _);
            _metric.RemoveLabelled(labels.Values);
        }
        finally
        {
            _lifetimeManagersLock.ExitWriteLock();
        }
    }
}