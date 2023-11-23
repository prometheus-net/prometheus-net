using System.Collections.Concurrent;

namespace Prometheus;

internal sealed class CollectorFamily
{
    public Type CollectorType { get; }

    public ConcurrentDictionary<CollectorIdentity, Collector> Collectors { get; } = new();

    public CollectorFamily(Type collectorType)
    {
        CollectorType = collectorType;
    }

    internal async Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
    {
        bool isFirst = true;

        // Iterate the pairs to avoid a defensive copy.
        foreach (var pair in Collectors)
        {
            await pair.Value.CollectAndSerializeAsync(serializer, isFirst, cancel);
            isFirst = false;
        }
    }
}
