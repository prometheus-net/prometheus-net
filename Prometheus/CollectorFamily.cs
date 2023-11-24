using System.Buffers;

namespace Prometheus;

internal sealed class CollectorFamily
{
    public Type CollectorType { get; }

    private readonly Dictionary<CollectorIdentity, Collector> _collectors = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public CollectorFamily(Type collectorType)
    {
        CollectorType = collectorType;
    }

    internal async Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
    {
        // The first family member we serialize requires different serialization from the others.
        bool isFirst = true;

        await ForEachCollectorAsync(async (collector, c) =>
        {
            await collector.CollectAndSerializeAsync(serializer, isFirst, cancel);
            isFirst = false;
        }, cancel);
    }

    internal Collector GetOrAdd<TCollector, TConfiguration>(CollectorIdentity identity, in CollectorRegistry.CollectorInitializer<TCollector, TConfiguration> initializer)
        where TCollector : Collector
        where TConfiguration : MetricConfiguration
    {
        // First we try just holding a read lock. This is the happy path.
        _lock.EnterReadLock();

        try
        {
            if (_collectors.TryGetValue(identity, out var collector))
                return collector;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        // Then we grab a write lock. This is the slow path. It could still be that someone beats us to it!

        _lock.EnterWriteLock();

        try
        {
            if (_collectors.TryGetValue(identity, out var collector))
                return collector;

            var newCollector = initializer.CreateInstance();
            _collectors.Add(identity, newCollector);
            return newCollector;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    internal void ForEachCollector(Action<Collector> action)
    {
        _lock.EnterReadLock();

        try
        {
            foreach (var collector in _collectors.Values)
                action(collector);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    internal async Task ForEachCollectorAsync(Func<Collector, CancellationToken, Task> func, CancellationToken cancel)
    {
        // This could potentially take nontrivial time, as we are serializing to a stream (potentially, a network stream).
        // Therefore we operate on a defensive copy in a reused buffer.
        Collector[] buffer;

        _lock.EnterReadLock();

        var collectorCount = _collectors.Count;
        buffer = ArrayPool<Collector>.Shared.Rent(collectorCount);

        try
        {
            try
            {
                _collectors.Values.CopyTo(buffer, 0);
            }
            finally
            {
                _lock.ExitReadLock();
            }

            for (var i = 0; i < collectorCount; i++)
            {
                var collector = buffer[i];
                await func(collector, cancel);
            }
        }
        finally
        {
            ArrayPool<Collector>.Shared.Return(buffer, clearArray: true);
        }
    }
}
