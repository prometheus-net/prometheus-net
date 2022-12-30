using System.Collections.Concurrent;

namespace Prometheus;

internal sealed class CollectorFamily
{
    public Type CollectorType { get; }

    // Different collectors in the same family are differentiated by different sets of static labels.
    public ConcurrentDictionary<LabelSequence, Collector> Collectors { get; } = new();

    public CollectorFamily(Type collectorType)
    {
        CollectorType = collectorType;
    }

    internal async Task CollectAndSerializeAsync(IMetricsSerializer serializer, CancellationToken cancel)
    {
        bool isFirst = true;

        foreach (var collector in Collectors.Values)
        {
            await collector.CollectAndSerializeAsync(serializer, isFirst, cancel);
            isFirst = false;
        }
    }
}
